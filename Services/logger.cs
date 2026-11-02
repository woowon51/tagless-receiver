using System;
using System.Diagnostics;
using System.IO;

namespace tagless_receiver
{
    public static class Logger
    {
        private static readonly string LogFolder = @"C:\TaglessReceiver";
        private static readonly string LogFile =
            Path.Combine(LogFolder, "tagless-receiver.log");

        public static void Write(string message)
        {
            try
            {
                // 폴더가 없으면 생성
                if (!Directory.Exists(LogFolder))
                {
                    Directory.CreateDirectory(LogFolder);
                }

                string line =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";

                // Visual Studio Output 창
                Debug.WriteLine(line);

                // 로그 파일
                File.AppendAllText(
                    LogFile,
                    line + Environment.NewLine
                );
            }
            catch
            {
                // 로그 기록 실패는 프로그램 동작에 영향을 주지 않음
            }
        }

        public static void Error(string message, Exception ex)
        {
            Write($"[ERROR] {message}");
            Write(ex.ToString());
        }

        /// <summary>Writes a MeHere trace without changing legacy Write/Error formatting.</summary>
        public static void Trace(
            string fileAlias,
            string className,
            string methodName,
            string eventName,
            string message = "",
            object? businessId = null,
            object? classId = null,
            object? businessMemberId = null,
            object? staffId = null,
            object? senderDeviceId = null,
            object? receiverDeviceId = null)
        {
            try
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                string timestamp = DateTimeOffset.UtcNow
                    .ToOffset(TimeSpan.FromHours(9))
                    .ToString("yyMMdd HH:mm:ss", culture);
                string line =
                    $"[MH b{Convert.ToString(businessId, culture)} c{Convert.ToString(classId, culture)} bm{Convert.ToString(businessMemberId, culture)} s{Convert.ToString(staffId, culture)} sd{Convert.ToString(senderDeviceId, culture)} rd{Convert.ToString(receiverDeviceId, culture)}] " +
                    $"{timestamp} {fileAlias}.{className}.{methodName} {eventName}" +
                    (string.IsNullOrEmpty(message) ? "" : " " + message);

                // Each sink is isolated so one failure does not suppress the other.
                try
                {
                    Debug.WriteLine(line);
                }
                catch
                {
                    // Logging must not interrupt application processing.
                }

                try
                {
                    if (!Directory.Exists(LogFolder))
                    {
                        Directory.CreateDirectory(LogFolder);
                    }
                    File.AppendAllText(LogFile, line + Environment.NewLine);
                }
                catch
                {
                    // Logging must not interrupt application processing.
                }
            }
            catch
            {
                // Includes timestamp and optional identifier formatting failures.
            }
        }
    }
}