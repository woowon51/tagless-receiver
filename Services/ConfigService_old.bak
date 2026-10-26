// config.json 존재 확인
// 없으면 생성
// 있으면 읽기

using System.Text.Json;
using tagless_receiver.Models;

namespace tagless_receiver.Services;

public static class ConfigService
{
    private static readonly string configFolder =
    @"C:\TaglessReceiver";

    private static readonly string configPath =
        Path.Combine(configFolder, "config.json");

    public static ReceiverConfig LoadOrCreate()
    {
        // config.json 이 있으면 읽기
        if (File.Exists(configPath))
        {
            string json = File.ReadAllText(configPath);

            ReceiverConfig? config =
                JsonSerializer.Deserialize<ReceiverConfig>(json);

            if (config != null)
                return config;
        }

        // config.json 이 없으면 새로 생성
        ReceiverConfig newConfig = new ReceiverConfig
        {
            receiver_device_id = Guid.NewGuid().ToString(),
            hardware_fingerprint = HardwareFingerprintService.Create(),
            business_id = null,
            class_id = null,
            registered = false,
            config_version = 1
        };

        // config.json 저장
        Save(newConfig);

        // 새로 생성한 config.json 반환
        return newConfig;
    }

    public static void Save(ReceiverConfig config)
    {
        // C:\TaglessReceiver  폴더가 없으면 생성
        Directory.CreateDirectory(configFolder);

        string json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(configPath, json);

    }
}