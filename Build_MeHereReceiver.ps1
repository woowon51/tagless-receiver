# Build locally only: no Git, deployment, installation or receiver execution.
[CmdletBinding()]
param([string]$InnoCompiler = 'D:\Program Files\Inno Setup 7\ISCC.exe')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ctx = @{ Root=$PSScriptRoot; Inno=$InnoCompiler }

function Test-BuildPrerequisites {
    $ctx.Project = Join-Path $ctx.Root 'tagless-receiver.csproj'
    $ctx.Profile = Join-Path $ctx.Root 'Properties\PublishProfiles\FolderProfile.pubxml'
    $parent = Split-Path $ctx.Root -Parent
    $ctx.Iss = Join-Path $parent 'Document\ISS_TRE_installer\MeHereReceiverSetup.iss'
    $ctx.OriginalIcon = Join-Path $parent 'Document\ISS_TRE_installer\MeHere.ico'
    $ctx.Icon = Join-Path $ctx.Root 'Resources\MeHere.ico'
    $ctx.Download = Join-Path $parent 'tagless-api\download'
    $ctx.TempRoot = Join-Path $parent 'TempBuild'
    $ctx.Dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    foreach ($path in @($ctx.Project,$ctx.Profile,$ctx.Iss,$ctx.OriginalIcon,$ctx.Icon,$ctx.Inno,$ctx.Download,$ctx.TempRoot)) {
        if (!(Test-Path -LiteralPath $path)) { throw "Required path missing: $path" }
    }
    if ([IO.Path]::GetPathRoot($ctx.TempRoot) -ne 'D:\') { throw 'Build workspace must be on D:' }
    [xml]$profile = [IO.File]::ReadAllText($ctx.Profile)
    $ctx.Framework = [string]$profile.Project.PropertyGroup.TargetFramework
    $ctx.Runtime = [string]$profile.Project.PropertyGroup.RuntimeIdentifier
    if ($ctx.Runtime -ne 'win-x64' -or [string]$profile.Project.PropertyGroup.Configuration -ne 'Release') { throw 'Expected Release/win-x64 profile' }
    $ctx.Publish = [IO.Path]::GetFullPath((Join-Path $ctx.Root ([string]$profile.Project.PropertyGroup.PublishDir)))
    if (!(Test-Path -LiteralPath $ctx.Publish)) { throw 'Existing Publish directory missing' }
    $issText = [IO.File]::ReadAllText($ctx.Iss)
    if (!$issText.Contains('Source: "'+$ctx.Publish.TrimEnd('\')+'\*"')) { throw 'ISS/Publish path mismatch' }
    [xml]$project = [IO.File]::ReadAllText($ctx.Project)
    if ([string]$project.Project.PropertyGroup.ApplicationIcon -ne 'Resources\MeHere.ico') { throw 'ApplicationIcon must reference Resources\MeHere.ico' }
    if ((Get-FileHash -LiteralPath $ctx.Icon).Hash -ne (Get-FileHash -LiteralPath $ctx.OriginalIcon).Hash) { throw 'Project/original ICO mismatch' }
    $ctx.Installed = Join-Path $env:ProgramFiles 'MeHere Receiver\tagless-receiver.exe'
    $ctx.Shortcut = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\MeHere Receiver.lnk'
    foreach ($path in @($ctx.Installed,$ctx.Shortcut,(Join-Path $ctx.Download 'MeHereReceiverSetup.exe'),(Join-Path $ctx.Download 'MeHereReceiverSetup.sha256'))) {
        if (!(Test-Path -LiteralPath $path)) { throw "Baseline file missing: $path" }
    }
    $ctx.Work = Join-Path $ctx.TempRoot ('MeHereReceiver_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[guid]::NewGuid().ToString('N').Substring(0,8))
}

function Backup-ReceiverArtifacts {
    $ctx.Backup = Join-Path $ctx.Work 'previous'
    New-Item -ItemType Directory -Path $ctx.Backup | Out-Null
    foreach ($name in @('MeHereReceiverSetup.exe','MeHereReceiverSetup.sha256')) {
        Copy-Item -LiteralPath (Join-Path $ctx.Download $name) -Destination (Join-Path $ctx.Backup $name)
        if ((Get-FileHash -LiteralPath (Join-Path $ctx.Download $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $ctx.Backup $name)).Hash) { throw 'Backup hash mismatch' }
    }
    Copy-Item -LiteralPath $ctx.Publish -Destination (Join-Path $ctx.Backup 'publish') -Recurse
    $ctx.Baseline = @{}
    foreach ($path in @($ctx.Iss,$ctx.Project,$ctx.Profile,$ctx.Icon,$ctx.OriginalIcon,$ctx.Installed,$ctx.Shortcut)) { $ctx.Baseline[$path]=(Get-FileHash -LiteralPath $path).Hash }
    $ctx.Baseline | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ctx.Work 'baseline.json') -Encoding UTF8
}

function Invoke-ReceiverPublish {
    & $ctx.Dotnet build $ctx.Project -c Release -r $ctx.Runtime --self-contained true -t:Rebuild -p:PublishProfile=FolderProfile 2>&1 | Tee-Object -FilePath (Join-Path $ctx.Work 'Rebuild.log')
    if ($LASTEXITCODE -ne 0) { throw 'Release Rebuild failed' }
    & $ctx.Dotnet publish $ctx.Project -c Release -r $ctx.Runtime -p:PublishProfile=FolderProfile --no-restore 2>&1 | Tee-Object -FilePath (Join-Path $ctx.Work 'Publish.log')
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    $ctx.Warnings = @(Select-String -LiteralPath (Join-Path $ctx.Work 'Rebuild.log') -Pattern 'warning CS\d+:.*' | ForEach-Object { $_.Matches.Value } | Sort-Object -Unique)
    $ctx.Exe = Join-Path $ctx.Publish 'tagless-receiver.exe'
    $ctx.Pdb = Join-Path $ctx.Publish 'tagless-receiver.pdb'
    $ctx.Assembly = Join-Path $ctx.Root ('bin\Release\'+$ctx.Framework+'\'+$ctx.Runtime+'\tagless-receiver.dll')
}

function Invoke-ReceiverRepack {
    if ((Get-FileHash -LiteralPath $ctx.Iss).Hash -ne $ctx.Baseline[$ctx.Iss]) { throw 'ISS changed during build' }
    $output = Join-Path $ctx.Work 'installer'
    New-Item -ItemType Directory -Path $output | Out-Null
    & $ctx.Inno ('/O'+$output) $ctx.Iss 2>&1 | Tee-Object -FilePath (Join-Path $ctx.Work 'Inno_Setup.log')
    if ($LASTEXITCODE -ne 0) { throw 'ISS Repack failed' }
    $ctx.Installer = Join-Path $output 'MeHereReceiverSetup.exe'
}

function Test-ReceiverArtifacts {
    $verify = Join-Path $ctx.Work 'VerifyArtifacts'
    New-Item -ItemType Directory -Path $verify | Out-Null
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>' | Set-Content -LiteralPath (Join-Path $verify 'VerifyArtifacts.csproj') -Encoding UTF8
    @"
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Buffers.Binary;
using var pdb = File.OpenRead(args[1]);
using var provider = MetadataReaderProvider.FromPortablePdbStream(pdb);
var reader = provider.GetMetadataReader();
foreach (var source in new[]{"ReceiverTray.cs","Services/InstallIdentity.cs","Services/RegistrationService.cs","Services/logger.cs"}) {
    var expected = SHA256.HashData(File.ReadAllBytes(Path.Combine(args[0],source)));
    var docs = reader.Documents.Where(h=>reader.GetString(reader.GetDocument(h).Name).Replace('\\','/').EndsWith("/"+source,StringComparison.OrdinalIgnoreCase)).ToArray();
    if(docs.Length!=1 || !reader.GetBlobBytes(reader.GetDocument(docs[0]).Hash).SequenceEqual(expected)) throw new Exception("Source/PDB mismatch: "+source);
    Console.WriteLine("PASS source/PDB: "+source);
}
using(var assembly=File.OpenRead(args[2])) using(var pe=new PEReader(assembly)) {
    var guid=new Guid(reader.DebugMetadataHeader!.Id.AsSpan(0,16));
    if(!pe.ReadDebugDirectory().Where(e=>e.Type==DebugDirectoryEntryType.CodeView).Any(e=>pe.ReadCodeViewDebugDirectoryData(e).Guid==guid)) throw new Exception("PDB/assembly identity mismatch");
}
var input=File.ReadAllBytes(args[2]);
var bundle=File.ReadAllBytes(args[3]);
if(bundle.AsSpan().IndexOf(input.AsSpan())<0) throw new Exception("Assembly missing from Publish bundle");
Console.WriteLine("PASS PDB/assembly/Publish bundle identity");
var ico=File.ReadAllBytes(args[5]);
if(BitConverter.ToUInt16(ico,0)!=0 || BitConverter.ToUInt16(ico,2)!=1) throw new Exception("Invalid ICO");
int count=BitConverter.ToUInt16(ico,4);
if(count==0) throw new Exception("Empty ICO");
foreach(var file in new[]{args[3],args[4]}) {
    using var stream=File.OpenRead(file);
    using var pe=new PEReader(stream);
    var resource=pe.PEHeaders.PEHeader!.ResourceTableDirectory;
    if(resource.RelativeVirtualAddress==0) throw new Exception("No PE resources: "+file);
    var tree=pe.GetSectionData(resource.RelativeVirtualAddress).GetContent(0,resource.Size).ToArray();
    Func<int,Dictionary<int,int>> directory=offset=> {
        int named=BitConverter.ToUInt16(tree,offset+12), ids=BitConverter.ToUInt16(tree,offset+14);
        var result=new Dictionary<int,int>();
        for(int i=0;i<named+ids;i++) {
            int at=offset+16+i*8; uint id=BitConverter.ToUInt32(tree,at), target=BitConverter.ToUInt32(tree,at+4);
            // Preserve named resources too: Inno Setup uses a named icon group.
            result.Add(unchecked((int)id),(int)(target&0x7fffffff));
        }
        return result;
    };
    Func<int,byte[]> payload=offset=> {
        int rva=BitConverter.ToInt32(tree,offset), size=BitConverter.ToInt32(tree,offset+4);
        return pe.GetSectionData(rva).GetContent(0,size).ToArray();
    };
    var roots=directory(0);
    if(!roots.ContainsKey(3)||!roots.ContainsKey(14)) throw new Exception("Icon resources missing: "+file);
    var images=directory(roots[3]); var groups=directory(roots[14]);
    if(groups.Count!=1 || images.Count!=count) throw new Exception("Unexpected icon group/image count: "+file);
    foreach(var group in groups.Values) foreach(var lang in directory(group)) {
        var data=payload(lang.Value);
        if(!data.AsSpan(0,6).SequenceEqual(ico.AsSpan(0,6))) throw new Exception("ICO/group header mismatch");
        for(int i=0;i<count;i++) {
            int source=6+i*16, entry=6+i*14;
            if(!data.AsSpan(entry,12).SequenceEqual(ico.AsSpan(source,12))) throw new Exception("Icon dimensions/format mismatch");
            int id=BitConverter.ToUInt16(data,entry+12);
            if(!images.ContainsKey(id)) throw new Exception("Missing icon image resource");
            var languages=directory(images[id]);
            if(!languages.TryGetValue(lang.Key,out var leaf)) throw new Exception("Icon language mismatch");
            var actual=payload(leaf);
            int size=BitConverter.ToInt32(ico,source+8), offset=BitConverter.ToInt32(ico,source+12);
            if(!actual.AsSpan().SequenceEqual(ico.AsSpan(offset,size))) throw new Exception("Icon image bytes differ from MeHere.ico");
        }
    }
    Console.WriteLine("PASS exact MeHere.ico match: "+Path.GetFileName(file)+" images="+count+" groups="+groups.Count);
}
"@ | Set-Content -LiteralPath (Join-Path $verify 'Program.cs') -Encoding UTF8
    & $ctx.Dotnet run --project (Join-Path $verify 'VerifyArtifacts.csproj') -c Release -- $ctx.Root $ctx.Pdb $ctx.Assembly $ctx.Exe $ctx.Installer $ctx.Icon 2>&1 | Tee-Object -FilePath (Join-Path $ctx.Work 'Artifact_verification.log')
    if ($LASTEXITCODE -ne 0) { throw 'Artifact/source/icon verification failed; deployment preserved' }
    foreach ($path in $ctx.Baseline.Keys) {
        if ((Get-FileHash -LiteralPath $path).Hash -ne $ctx.Baseline[$path]) { throw "Baseline file changed unexpectedly: $path" }
    }
    if ((Get-FileHash -LiteralPath $ctx.Installer).Hash -eq (Get-FileHash -LiteralPath (Join-Path $ctx.Backup 'MeHereReceiverSetup.exe')).Hash) { throw 'Installer unchanged' }
    $ctx.InstallerHash = (Get-FileHash -LiteralPath $ctx.Installer).Hash.ToLowerInvariant()
    $ctx.ExeHash = (Get-FileHash -LiteralPath $ctx.Exe).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $ctx.Work 'Publish_EXE.sha256'),$ctx.ExeHash+"`n",[Text.Encoding]::ASCII)
    [IO.File]::WriteAllText((Join-Path (Split-Path $ctx.Installer -Parent) 'MeHereReceiverSetup.sha256'),$ctx.InstallerHash+"`n",[Text.Encoding]::ASCII)
    $ctx.Verified = $true
}

function Update-ReceiverDownload {
    if (!$ctx.Verified) { throw 'Cannot deploy unverified artifacts' }
    $stage = Join-Path $ctx.Work 'stage'
    New-Item -ItemType Directory -Path $stage | Out-Null
    $names = @('MeHereReceiverSetup.exe','MeHereReceiverSetup.sha256')
    Copy-Item -LiteralPath $ctx.Installer -Destination (Join-Path $stage $names[0])
    [IO.File]::WriteAllText((Join-Path $stage $names[1]),$ctx.InstallerHash+"`n",[Text.Encoding]::ASCII)
    if ((Get-FileHash -LiteralPath (Join-Path $stage $names[0])).Hash.ToLowerInvariant() -ne [IO.File]::ReadAllText((Join-Path $stage $names[1])).Trim()) { throw 'Staged checksum mismatch' }
    foreach ($name in $names) {
        if ((Get-FileHash -LiteralPath (Join-Path $ctx.Download $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $ctx.Backup $name)).Hash) { throw 'Deployment files changed during build' }
    }
    try {
        foreach ($name in $names) { [IO.File]::Replace((Join-Path $stage $name),(Join-Path $ctx.Download $name),(Join-Path $stage ('replaced-'+$name))) }
        if ((Get-FileHash -LiteralPath (Join-Path $ctx.Download $names[0])).Hash.ToLowerInvariant() -ne $ctx.InstallerHash -or [IO.File]::ReadAllText((Join-Path $ctx.Download $names[1])).Trim() -ne $ctx.InstallerHash) { throw 'Final download integrity check failed' }
        $ctx.Replaced=$true
    } catch {
        foreach ($name in $names) {
            Copy-Item -LiteralPath (Join-Path $ctx.Backup $name) -Destination (Join-Path $ctx.Download $name) -Force
            if ((Get-FileHash -LiteralPath (Join-Path $ctx.Download $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $ctx.Backup $name)).Hash) { throw 'CRITICAL: deployment rollback verification failed' }
        }
        throw
    }
}

Test-BuildPrerequisites
New-Item -ItemType Directory -Path $ctx.Work | Out-Null
Start-Transcript -Path (Join-Path $ctx.Work 'Build.log') | Out-Null
$envNames=@('TEMP','TMP','DOTNET_CLI_HOME','DOTNET_NOLOGO','DOTNET_SKIP_FIRST_TIME_EXPERIENCE')
$oldEnv=@{}
foreach($name in $envNames) { $oldEnv[$name]=[Environment]::GetEnvironmentVariable($name,'Process') }
try {
    Write-Output ('WORK='+$ctx.Work)
    New-Item -ItemType Directory -Path (Join-Path $ctx.Work 'tmp') | Out-Null
    $env:TEMP=Join-Path $ctx.Work 'tmp'; $env:TMP=$env:TEMP
    $env:DOTNET_CLI_HOME=Join-Path $ctx.Work 'dotnet-home'; $env:DOTNET_NOLOGO='1'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
    Backup-ReceiverArtifacts
    Invoke-ReceiverPublish
    Invoke-ReceiverRepack
    Test-ReceiverArtifacts
    Update-ReceiverDownload
    $file=Get-Item -LiteralPath $ctx.Installer
    $result=[ordered]@{result='PASS';work=$ctx.Work;publish=$ctx.Publish;publish_exe_sha256=(Get-FileHash -LiteralPath $ctx.Exe).Hash;installer_bytes=$file.Length;installer_modified_kst=$file.LastWriteTime.ToString('o');installer_sha256=$ctx.InstallerHash;icon_sha256=(Get-FileHash -LiteralPath $ctx.Icon).Hash;exe_icon_exact_match=$true;installer_icon_exact_match=$true;icon_images=[BitConverter]::ToUInt16([IO.File]::ReadAllBytes($ctx.Icon),4);sources_pdb_bundle_match=$true;download_replaced=$true;warnings=$ctx.Warnings;installed_exe_unchanged=$true;shortcut_unchanged=$true;iss_unchanged=$true}
    $result|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $ctx.Work 'result.json') -Encoding UTF8
    $result|ConvertTo-Json -Depth 4
} catch {
    if ($ctx.ContainsKey('Replaced') -and $ctx.Replaced) {
        foreach ($name in @('MeHereReceiverSetup.exe','MeHereReceiverSetup.sha256')) {
            Copy-Item -LiteralPath (Join-Path $ctx.Backup $name) -Destination (Join-Path $ctx.Download $name) -Force
            if ((Get-FileHash -LiteralPath (Join-Path $ctx.Download $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $ctx.Backup $name)).Hash) { throw 'CRITICAL: deployment rollback verification failed' }
        }
    }
    [ordered]@{result='FAIL';error=$_.Exception.Message;work=$ctx.Work}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $ctx.Work 'result.json') -Encoding UTF8
    throw
} finally {
    foreach($name in $envNames) { [Environment]::SetEnvironmentVariable($name,$oldEnv[$name],'Process') }
    Stop-Transcript | Out-Null
}