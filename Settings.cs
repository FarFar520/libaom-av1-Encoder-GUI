namespace 破片压缩器 {
    public static class Settings {
        public static bool b编码后删除切片 = false;
        public static bool b转码成功后删除源视频 = false;

        public static bool b多线程 = false;

        public static int i分割GOP = 2;
        public static int sec_gop = 5;

        public static int i降噪强度 = 4;

        public static float crf = 32;

        public static bool b自定义滤镜 = false;
        public static string str自定义滤镜 = string.Empty;

        public static bool b扫描场景 = false;
        public static bool b无缓转码 = false;

        public static int i声道 = 2, i音频码率 = 96, i音轨保留;
        public static bool b音轨同时切片转码 = false, opus = false;

        public static bool b手动剪裁 = false;
        public static int i剪裁后宽, i剪裁后高, i左裁像素, i上裁像素;//剪裁按比例计算

        public static bool b右上角文件名_切片序列号水印 = false;

        public static int i最小边长 = 64;
        static int _i缩小到宽, _i缩小到高;//缩小是绝对值

        public static bool b长边像素 = false;
        public static int i长边 = 0;

        public static int i缩小到宽 {
            set { _i缩小到宽 = value < i最小边长 ? -4 : value; }
            get { return _i缩小到宽; }
        }
        public static int i缩小到高 {
            set { _i缩小到高 = value < i最小边长 ? -4 : value; }
            get { return _i缩小到高; }
        }

        public static bool b硬字幕;
        public static bool b转可变帧率;

        public static bool b磨皮降噪, b自动裁黑边;
        public static bool b非对称剪裁 => i左裁像素 > 0 || i上裁像素 > 0;
        public static bool b四周剪裁 => i剪裁后宽 > i最小边长 && i剪裁后高 > i最小边长;
        public static bool b缩小到指定分辨率(int i显示宽, int i显示高, out int i缩放宽, out int i缩放高) {
            i缩放宽 = i显示宽;
            i缩放高 = i显示高;


            if (_i缩小到宽 > i最小边长 && _i缩小到高 > i最小边长) {
                if (i缩放宽 != _i缩小到宽 || i缩放高 != _i缩小到高) {
                    i缩放宽 = _i缩小到宽;
                    i缩放高 = _i缩小到高;
                    return true;
                }
            }
            return false;

        }

        public static bool b宽度缩小 => _i缩小到宽 > i最小边长;
        public static bool b高度缩小 => _i缩小到高 > i最小边长;

        public static string str音频编码格式;

        public static bool b以DAR比例修正 = true;

        public static string str选择预设;

        public static bool b根据帧率自动强化CRF = true;

        public static string str缩小文本 {
            get {
                string str文本 = b以DAR比例修正 ? ",DAR修正" : "";

                if (b长边像素) {
                    if (i长边 > i最小边长)
                        str文本 = $"长边{i长边}像素";
                } else {
                    if (i缩小到宽 > i最小边长 && i缩小到高 > i最小边长) {
                        str文本 = $"{i缩小到宽}×{i缩小到高}{str文本}";
                    } else if (i缩小到宽 > i最小边长) {
                        str文本 = $"{i缩小到宽}×自动高{str文本}";
                    } else if (i缩小到高 > i最小边长) {
                        str文本 = $"自动宽×{i缩小到高}{str文本}";
                    } else {
                        str文本 = $"不缩放{str文本}";
                    }
                }
                return str文本;
            }
        }

        public static string opus摘要 {
            get {
                if (i声道 == 2)
                    return $".opus2.0.{i音频码率}k";
                else if (i声道 == 1)
                    return $".opus1.0.{i音频码率}k";
                else
                    return $".opus.{i音频码率}k";
            }
        }

        public static LibEnc lib已设置;

        public static string Get_视频编码库(VideoInfo info, out LibEnc.命令行 v命令行) {//设置参数手工刷新，再传入编码库拼接。
            return lib已设置.get压视频参数(info, str选择预设, b多线程, b磨皮降噪, b根据帧率自动强化CRF, crf, (ushort)i降噪强度, out v命令行);

        }
    }
}
