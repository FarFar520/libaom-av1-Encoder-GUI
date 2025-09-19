using System;
using System.Diagnostics;
using System.IO;

namespace 破片压缩器 {
    internal static class EXE {
        public const string ffmpeg单线程 = "-threads 1 -filter_threads 1 -filter_complex_threads 1 ";
        public const string ffmpeg单线程解码 = "-threads 1 ";
        public const string ffmpeg不显库 = "-hide_banner ";

        static DateTime time上次查找ffmpeg = DateTime.Now.AddDays(1);
        public static string ffmpeg = "ffmpeg", ffprobe = "ffprobe", mkvmerge = "mkvmerge", mkvextract = "mkvextract";//运行库默认
        public static bool find最新版ffmpeg(out string exe) {
            DateTime time查找 = DateTime.Now;
            if (Math.Abs(time查找.Subtract(time上次查找ffmpeg).TotalSeconds) > 60) {
                time上次查找ffmpeg = time查找;
                try {
                    string[] exeFiles = Directory.GetFiles(Environment.CurrentDirectory, "*ffmpeg*.exe");
                    FileInfo fi时间最近 = new FileInfo(exeFiles[0]);
                    for (int i = 1; i < exeFiles.Length; i++) {
                        FileInfo fi = new FileInfo(exeFiles[i]);
                        if (fi.Length > 100000000 && fi.LastWriteTime > fi时间最近.LastWriteTime) {
                            fi时间最近 = fi;
                        }
                    }
                    ffmpeg = fi时间最近.Name;
                } catch { }
            }
            exe = ffmpeg;
            return try_ffmpeg程序可运行(exe, "ffmpeg");
        }//只查找程序同目录新增ffmpeg
        public static bool find最新版ffprobe(out string exe) {
            try {
                string[] exeFiles = Directory.GetFiles(Environment.CurrentDirectory, "*ffprobe*.exe");
                FileInfo fi时间最近 = new FileInfo(exeFiles[0]);
                for (int i = 1; i < exeFiles.Length; i++) {
                    FileInfo fi = new FileInfo(exeFiles[i]);
                    if (fi.Length > 100000000 && fi.LastWriteTime > fi时间最近.LastWriteTime) {
                        fi时间最近 = fi;
                    }
                }
                ffprobe = fi时间最近.Name;
            } catch { }
            exe = ffprobe;
            return try_ffmpeg程序可运行(exe, "ffprobe");
        }
        public static bool find最新版mkvmerge(out string exe) {
            try {
                string[] exeFiles = Directory.GetFiles(Environment.CurrentDirectory, "*mkvmerge*.exe");

                FileInfo fi时间最近 = new FileInfo(exeFiles[0]);
                for (int i = 1; i < exeFiles.Length; i++) {
                    FileInfo fi = new FileInfo(exeFiles[i]);
                    if (fi.Length > 10000000 && fi.LastWriteTime > fi时间最近.LastWriteTime) {
                        fi时间最近 = fi;
                    }
                }
                mkvmerge = fi时间最近.Name;
            } catch { }
            exe = mkvmerge;
            return try_mkvmerge程序可运行(exe, "mkvmerge");
        }
        public static bool find最新版mkvextract(out string exe) {
            try {
                string[] exeFiles = Directory.GetFiles(Environment.CurrentDirectory, "*mkvextract*.exe");
                FileInfo fi时间最近 = new FileInfo(exeFiles[0]);
                for (int i = 1; i < exeFiles.Length; i++) {
                    FileInfo fi = new FileInfo(exeFiles[i]);
                    if (fi.Length > 10000000 && fi.LastWriteTime > fi时间最近.LastWriteTime) {
                        fi时间最近 = fi;
                    }
                }
                mkvextract = fi时间最近.Name;
            } catch { }
            exe = mkvextract;
            return try_mkvmerge程序可运行(exe, "mkvextract");
        }
        static bool try_ffmpeg程序可运行(string exe, string 开头) {
            using (Process process = new Process( )) {
                process.StartInfo.FileName = exe;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardError = true;
                try { process.Start( ); } catch { return false; }
                string err = process.StandardError.ReadToEnd( );
                if (err.StartsWith(开头) && err.Contains("libavcodec")) {
                    return true;
                }
            }
            return false;
        }
        static bool try_mkvmerge程序可运行(string exe, string 开头) {
            using (Process process = new Process( )) {
                process.StartInfo.FileName = exe;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                try { process.Start( ); } catch { return false; }
                string err = process.StandardOutput.ReadToEnd( );
                if (err.StartsWith(开头)) {
                    return true;
                }
            }
            return false;
        }

    }
}
