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
    }
}