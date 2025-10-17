using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace 破片压缩器 {
    public class VideoInfo {

        public int i显示宽比 = 1, i显示高比 = 1;

        public bool b畸变与修正 = false;
        public int i片源帧宽 = 1, i片源帧高 = 1;
        public float DAR = 1.0f;
        //PAR - pixel aspect ratio，单个像素的宽高比，大多数情况像素宽高比为1:1，也就是一个正方形像素，如果不是1:1， 则该像素可以理解为长方形像素。常用的PAR比率有(1:1，10:11, 40:33, 16:11, 12:11 ).
        //DAR - display aspect ratio，显示宽高比。即最终播放出来的画面的宽与高之比。比如常见的16:9和4:3等。缩放视频也要按这个比例来，否则会使图像看起来被压扁或者拉长了似的。
        //SAR - Sample aspect ratio，采样纵横比， 表示横向的像素点数和纵向的像素点数的比值，即为我们通常提到的分辨率宽高比。就是对图像采集时，横向采集与纵向采集构成的点阵，横向点数与纵向点数的比值。比如VGA图像640/480 = 4:3，D-1 PAL图像720/576 = 5:4，高清视频 等。

        public int i显示帧宽 = 1;

        public int i输出宽 = 1, i输出高 = 1, i输出长边 = 1, i输出短边 = 1,i输出像素=1;
        public float f输入帧率 = 23.976f;
        public float f输出帧率 = 1.0f;
        public float f输入每帧秒 = 0.041708f;

        public bool b隔行扫描 = false;
        public bool b剪裁滤镜 = false, b缩放滤镜 = false, b改变了尺寸 = false;

        public string str剪裁滤镜, str缩放滤镜;

        public int inSumFrame = 1, outSumFrames = 1;
        public int sum_interlaced_frame = 0;

        public List<string> list信息流 = new List<string>( );

        public List<int> list视频轨 = new List<int>( )
               , list音频轨 = new List<int>( )
               , list字幕轨 = new List<int>( )
               , list图片轨 = new List<int>( )
               , list其它轨 = new List<int>( );


        public static Regex regexWH = new Regex(@"(?<w>[1-9]\d+)x(?<h>[1-9]\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Regex regexFPS = new Regex(@"(?<fps>\d+(\.\d+)?) fps", RegexOptions.IgnoreCase | RegexOptions.Compiled);//总帧数÷总时长（平均帧率）
        public static Regex regexTBR = new Regex(@"(?<tbr>\d+(\.\d+)?) tbr", RegexOptions.IgnoreCase | RegexOptions.Compiled);//基准帧率
        public static Regex regexDAR = new Regex(@"DAR\s*(?<darW>\d+):(?<darH>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);//备用
        public static Regex regexAudio = new Regex(@"Audio: (?<code>\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Regex regex隔行扫描 = new Regex(@"(top|bottom)\s+first", RegexOptions.IgnoreCase | RegexOptions.Compiled);//交错视频

        public 输出 OUT = new 输出( );
        public 输入 IN = new 输入( );
        public 剪裁参数 黑边剪裁 = new 剪裁参数( );
        public 剪裁参数 手动剪裁 = new 剪裁参数( );

        public FileInfo fileInfo;
        public string str视频名无后缀;

        public TimeSpan time视频时长 = TimeSpan.Zero;

        string _str长乘宽;
        public string str长乘宽 => _str长乘宽;
        public string get短边Progressive {
            get {
                if (i输出短边 > 100) {
                    return $"{i输出短边 - i输出短边 % 10}p";
                } else
                    return $"{i输出短边}p";
            }
        }
        public string get输出Progressive {
            get {
                double K = Math.Round(i输出长边 / 1000.0f, 1);
                if (i输出长边 % 960 == 0) K = i输出长边 / 960;
                else if (i输出长边 % 1024 == 0) K = i输出长边 / 1024;
                string progressive = string.Empty;
                if (i输出长边 / 1000.0f > 4.096) {
                    progressive = $"{K}K";
                } else {
                    if (i输出长边 >= 3840) progressive = "2160p";
                    else if (i输出长边 >= 2560) progressive = "1440p";
                    else if (i输出长边 >= 1920) progressive = "1080p";
                    else if (i输出长边 >= 1440) progressive = "810p";
                    else if (i输出长边 >= 1280) progressive = "720p";
                    else if (i输出长边 >= 960) progressive = "540p";
                    else if (i输出长边 >= 640) progressive = "360p";
                    else if (i输出长边 >= 480) progressive = "270p";
                    else if (i输出长边 >= 320) progressive = "240p";
                    else if (i输出长边 >= 240) progressive = "180p";
                    else if (i输出长边 >= 180) progressive = "135p";
                    else if (i输出长边 >= 120) progressive = "90p";
                    else if (i输出长边 >= 64) progressive = "48p";
                    else progressive = $"{i输出长边 * 9 / 16}p";
                }
                return progressive;
            }
        }

        public string get音轨code {
            get {
                for (int i = 0; i < list音频轨.Count; i++) {
                    if (regexAudio.IsMatch(list信息流[list音频轨[i]])) {
                        return "." + regexAudio.Match(list信息流[list音频轨[i]]).Groups["code"].Value;
                    }
                }
                return string.Empty;
            }
        }

        public class 剪裁参数 {
            public bool b触发 = false;
            public string crop;
            public int width, height, left_x, top_y;

            public void split(string crop, int i片源帧宽, int i片源帧高) {
                string[] arr_w_h_x_y = crop.Substring(5).Split(':');
                int.TryParse(arr_w_h_x_y[0], out int w);
                int.TryParse(arr_w_h_x_y[1], out int h);

                if (w > Settings.i最小边长 && w >= width && h > Settings.i最小边长 && h >= height) {
                    width = w; height = h;

                    if (width >= i片源帧宽 && height >= i片源帧高) {//剪裁尺寸和源片尺寸一致，清空剪裁
                        b触发 = false;
                        this.crop = string.Empty;
                    } else {
                        b触发 = true;
                        this.crop = crop;
                        int.TryParse(arr_w_h_x_y[2], out left_x);
                        int.TryParse(arr_w_h_x_y[3], out top_y);
                    }
                }
            }
            public void 判断赋值(int i显示帧宽, int i片源帧宽, int i片源帧高) {
                if (Settings.b四周剪裁) {
                    if (Settings.i剪裁后宽 <= i片源帧宽 && Settings.i剪裁后高 <= i片源帧高) {
                        if (Settings.i剪裁后宽 == i片源帧宽 && Settings.i剪裁后高 == i片源帧高) return;

                        b触发 = true;
                        width = Settings.i剪裁后宽;
                        height = Settings.i剪裁后高;
                        top_y = Settings.i上裁像素;
                        left_x = Settings.i左裁像素;
                    } else if (Settings.i剪裁后宽 <= i显示帧宽 && Settings.i剪裁后高 <= i片源帧高) {
                        if (Settings.i剪裁后宽 == i显示帧宽 && Settings.i剪裁后高 == i片源帧宽) return;

                        b触发 = true;

                        height = Settings.i剪裁后高;
                        top_y = Settings.i上裁像素;

                        width = Settings.i剪裁后宽 * i片源帧宽 / i显示帧宽;
                        left_x = Settings.i左裁像素 * i片源帧宽 / i显示帧宽;
                    }

                    if (left_x > 0 || top_y > 0)
                        crop = $"crop={width}:{height}:{left_x}:{top_y}";
                    else crop = $"crop={width}:{height}";
                }
            }

            public void 不可查觉恢复宽高(int i片源帧宽, int i片源帧高, out int i放大到宽, out int i放大到高) {
                float f不可察觉比 = (1920.0f - 32) / 1920;
                if (i片源帧宽 * f不可察觉比 <= width && i片源帧高 * f不可察觉比 <= height) {
                    i放大到宽 = i片源帧宽;
                    i放大到高 = i片源帧高;
                } else {
                    i放大到宽 = width;
                    i放大到高 = height;
                }
            }
        }

        public class 输出 {
            public bool b抽重复帧 = false;
            public float adjust_crf = 0;
            public string enc = string.Empty, str量化名 = "crf", preset = string.Empty, str视流格式 = string.Empty, denoise = string.Empty;
        }
        public class 输入 {
            public float f单核解码能力 = float.MaxValue;//优先使用单线程解码，减少线程间通讯损耗。
            public string ffmpeg单线程解码 = EXE.ffmpeg单线程;
        }

        public VideoInfo(FileInfo fileInfo) {
            this.fileInfo = fileInfo;
            str视频名无后缀 = fileInfo.Name.Substring(0, fileInfo.Name.LastIndexOf("."));
        }

        public List<string> get音频轨信息list( ) {
            List<string> list = new List<string>( );
            foreach (int a in list音频轨) {
                list.Add(list信息流[a]);
            }
            return list;
        }
        public List<string> get字幕轨信息List( ) {
            List<string> list = new List<string>( );
            foreach (int a in list字幕轨) {
                list.Add(list信息流[a]);
            }
            return list;
        }
        public void fx信息分类(string line) {
            if (!string.IsNullOrWhiteSpace(line)) {
                line = line.Trim( );
                list信息流.Add(line);

                if (line.StartsWith("Stream #0")) {
                    int i轨道号 = list信息流.Count - 1;
                    if (line.EndsWith("pic)")) {
                        list图片轨.Add(i轨道号);
                    } else if (line.IndexOf("Video:", 11) > 11) {
                        list视频轨.Add(i轨道号);
                        v匹配视频信息(line, i轨道号);
                    } else if (line.IndexOf("Audio:", 11) > 11) {
                        list音频轨.Add(i轨道号);
                    } else if (line.IndexOf("Subtitle:", 11) > 11) {
                        list字幕轨.Add(i轨道号);
                    } else {
                        list其它轨.Add(i轨道号);
                    }
                } else if (line.StartsWith("Duration: ", StringComparison.OrdinalIgnoreCase)) {
                    int len = line.IndexOf(',', 11) - 11;
                    if (len > 0) {
                        if (TimeSpan.TryParse(line.Substring(11, len), out TimeSpan timeSpan)) {
                            if (timeSpan > TimeSpan.Zero) {
                                time视频时长 = timeSpan;
                            }
                        }
                    }
                }
            }
        }

        void v匹配视频信息(string line, int i轨道号) {
            Match matchWH = regexWH.Match(line);
            if (matchWH.Success) {
                //Stream #0:1[0x2](und): Video: h264 (High) (avc1 / 0x31637661), yuv420p(tv, smpte170m), 1440x1080 [SAR 4:3 DAR 16:9], 6506 kb/s, 29.97 fps, 29.97 tbr, 60k tbn (default)
                if (int.TryParse(matchWH.Groups["w"].Value, out int w) && int.TryParse(matchWH.Groups["h"].Value, out int h)) {
                    if (w * h > i片源帧宽 * i片源帧高) {
                        i片源帧宽 = w; i片源帧高 = h;
                        Match mathDAR = regexDAR.Match(line);

                        if (int.TryParse(mathDAR.Groups["darW"].Value, out int dar_w) && dar_w > 0) {
                            i显示宽比 = dar_w;
                        } else {
                            i显示宽比 = w;
                        }
                        if (int.TryParse(mathDAR.Groups["darH"].Value, out int dar_h) && dar_h > 0) {
                            i显示高比 = dar_h;
                        } else {
                            i显示高比 = h;
                        }
                        DAR = ((float)i显示宽比) / i显示高比;
                        i显示帧宽 = i片源帧高 * i显示宽比 / i显示高比;
                        b畸变与修正 = Settings.b以DAR比例修正 && i显示帧宽 != i片源帧宽;
                    }
                }
            }

            if (regex隔行扫描.IsMatch(line)) b隔行扫描 = true;//隔行则不需要变动，非隔行则判断

            if (float.TryParse(regexFPS.Match(line).Groups["fps"].Value, out float f) && f > 0) {
                f输入帧率 = f;
            }
            if (float.TryParse(regexTBR.Match(line).Groups["tbr"].Value, out float tbr) && tbr > 0) {//tbr应该是显示帧率，包含时间码的。
                f输入帧率 = tbr;
            }

            f输出帧率 = b隔行扫描 ? f输入帧率 * 2 : f输入帧率;
            f输入每帧秒 = 1 / f输入帧率;
            inSumFrame = (int)(f输入帧率 * time视频时长.TotalSeconds);
            outSumFrames = (int)(f输出帧率 * time视频时长.TotalSeconds);
        }

        public void fx充分剪裁匹配(Dictionary<string, int> crops) {
            int min_w = int.MaxValue, min_h = int.MinValue;
            int lx = 0, ty = 0;
            foreach (string c in crops.Keys) {
                if (crops[c] > 5 * f输入帧率) {
                    string[] arr_w_h_x_y = c.Substring(5).Split(':');
                    int.TryParse(arr_w_h_x_y[0], out int w);
                    int.TryParse(arr_w_h_x_y[1], out int h);
                    int.TryParse(arr_w_h_x_y[2], out int left_x);
                    int.TryParse(arr_w_h_x_y[3], out int top_y);
                    if (w > min_w || h > min_h) {
                        min_w = w; min_h = h; lx = left_x; ty = top_y;
                    }
                }
            }
        }
        public void fx匹配剪裁(Dictionary<string, int> crops, uint count_Crops) {
            KeyValuePair<string, int> mostCrop = new KeyValuePair<string, int>( );
            KeyValuePair<string, int> secondCrop = new KeyValuePair<string, int>( );
            if (crops.Count > 0) secondCrop = crops.Last( );
            else return;

            foreach (KeyValuePair<string, int> kvp in crops) {
                if (mostCrop.Value < kvp.Value) {
                    secondCrop = mostCrop;
                    mostCrop = kvp;
                } else if (secondCrop.Value < kvp.Value) {
                    secondCrop = kvp;
                }
            }
            if (mostCrop.Key != null) {
                if (secondCrop.Key != null && secondCrop.Value >= 6 * f输入帧率) {//备选裁剪尺寸，当备选剪裁尺寸大于首选，启用备选尺寸。
                    黑边剪裁.split(secondCrop.Key, i片源帧宽, i片源帧高);
                } else {
                    黑边剪裁.split(mostCrop.Key, i片源帧宽, i片源帧高);
                }
            }
        }

        public void fx输出宽高( ) {
            i输出宽 = i片源帧宽;
            i输出高 = i片源帧高;
            str缩放滤镜 = string.Empty;

            if (Settings.b手动剪裁)
                手动剪裁.判断赋值(i显示帧宽, i片源帧宽, i片源帧高);

            if (手动剪裁.b触发) {
                str剪裁滤镜 = 手动剪裁.crop;
                i输出宽 = 手动剪裁.width;
                i输出高 = 手动剪裁.height;
            } else if (黑边剪裁.b触发) {
                str剪裁滤镜 = 黑边剪裁.crop;
                黑边剪裁.不可查觉恢复宽高(i片源帧宽, i片源帧高, out i输出宽, out i输出高);
            } else
                str剪裁滤镜 = string.Empty;

            int i_剪后宽 = i输出宽;
            int i_剪后高 = i输出高;

            int display_width = i片源帧高 * i显示宽比 / i显示高比 * i输出宽 / i片源帧宽; //46.341K×46.341K视频会溢出。
            if (Settings.b以DAR比例修正) {
                i输出宽 = display_width;
            }

            if (Settings.b缩小到指定分辨率(display_width, i输出高, out int w, out int h)) {
                i输出宽 = w;
                i输出高 = h;
            } else if (Settings.b长边像素 && Settings.i长边 > Settings.i最小边长) {//以长边为基准缩小。
                if (i输出宽 > i输出高 && i输出宽 > Settings.i长边) {
                    h = i输出高 * Settings.i长边 / i输出宽;
                    i输出高 = b畸变与修正 ? i输出高 * Settings.i长边 / i输出宽 : -2;//不可更改代码顺序
                    w = i输出宽 = Settings.i长边;
                } else if (i输出高 > Settings.i长边) {
                    w = i输出宽 * Settings.i长边 / i输出高;//不可更改代码顺序
                    i输出宽 = b畸变与修正 ? i输出宽 * Settings.i长边 / i输出高 : -2;//不可更改代码顺序
                    h = i输出高 = Settings.i长边;
                }
            } else if (Settings.b宽度缩小 && i输出宽 >= Settings.i缩小到宽) {
                h = (int)((float)Settings.i缩小到宽 / i输出宽 * i输出高);
                i输出高 = b畸变与修正 ? i输出高 * Settings.i缩小到宽 / i输出宽 : Settings.i缩小到高;//不可更改代码顺序
                w = i输出宽 = Settings.i缩小到宽;
            } else if (Settings.b高度缩小 && i输出高 >= Settings.i缩小到高) {
                w = (int)((float)Settings.i缩小到高 / i输出高 * i输出宽);
                i输出宽 = b畸变与修正 ? i输出宽 * Settings.i缩小到高 / i输出高 : Settings.i缩小到宽;//不可更改代码顺序
                h = i输出高 = Settings.i缩小到高;
            } else {
                w = i输出宽;
                h = i输出高;
            }

            //int modW = i输出宽 % 4;// 1、2像素缩小、3像素放大1像素。  
            //if (modW > 0) {
            //    if (modW > 2) i输出宽 = i输出宽 - modW + 4;
            //    else i输出宽 = i输出宽 - modW;
            //    w = i输出宽;
            //}//以4×4为最小分辨率，x265高压最低切块要求
            //int modH = i输出高 % 2;
            //if (modH > 0) {
            //    if (modH > 2) i输出高 = i输出高 - modH + 4;
            //    else i输出高 = i输出高 - modH;
            //    h = i输出高;
            //}//以4×4为最小分辨率，x265高压最低切块要求

            if (i输出宽 > 0 && i输出宽 % 2 == 1) w = ++i输出宽;
            else if (w % 2 == 1) w++;

            if (i输出高 > 0 && i输出高 % 2 == 1) h = ++i输出高;//以2×2为最小分辨率，libsvt-av1、libaomav1支持2×2像素块
            else if (h % 2 == 1) h++;

            if (w > h) {
                i输出长边 = w;
                i输出短边 = h;
            } else {
                i输出长边 = h;
                i输出短边 = w;
            }
            i输出像素 = w * h;
            _str长乘宽 = $"{w}×{h}";

            if ((i输出宽 > Settings.i最小边长 && i输出宽 != i_剪后宽) || (i输出高 > Settings.i最小边长 && i输出高 != i_剪后高)) {
                str缩放滤镜 = $"scale={i输出宽}:{i输出高}:flags=lanczos";
                //str缩放滤镜 = $"scale={i输出宽}:{i输出高}:flags=bicubic";//lanczos硬、bicubic柔
                //   The default scaling flags wouldn't be applied, so the default scaling algorithm would be "bilinear" instead of "bicubic". [ Changed to "bicubic" since 9f14396a5103ec80893db801035ece5d14c0d3c5. ]͏    To achieve the same: specify the algorithm via "flags=bicubic" alike.
            }

            b剪裁滤镜 = !string.IsNullOrEmpty(str剪裁滤镜);
            b缩放滤镜 = !string.IsNullOrEmpty(str缩放滤镜);

            b改变了尺寸 = b剪裁滤镜 || b缩放滤镜;
        }

        public void v以帧判断隔行扫描(string[] Data) {
            int scan_frame = 0;
            for (int i = 0; i < Data.Length; i++) {
                if (Data[i] == "interlaced_frame=1") {
                    sum_interlaced_frame++;
                } else if (Data[i] == "interlaced_frame=0")
                    scan_frame++;
            }
            b隔行扫描 = scan_frame <= sum_interlaced_frame;
            f输出帧率 = b隔行扫描 ? f输入帧率 * 2 : f输入帧率;
        }
        public void v以帧判断隔行扫描(int scan_frame, List<string> Data) {
            for (int i = 0; i < Data.Count; i++) {
                if (Data[i] == "interlaced_frame=1") {
                    sum_interlaced_frame++;
                }
            }
            b隔行扫描 = scan_frame - sum_interlaced_frame <= sum_interlaced_frame;
            f输出帧率 = b隔行扫描 ? f输入帧率 * 2 : f输入帧率;
        }

    }

}

