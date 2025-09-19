using System.Collections.Generic;

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

        public static string Get_视频编码库(VideoInfo info, out string str多线程编码库) {
            //设置参数手工刷新，再传入编码库拼接。
            //info.OUT.enc = lib已设置.value编码库;
            return lib已设置.get压视频参数(info, new List<string>( ), str选择预设, b多线程, b磨皮降噪, b根据帧率自动强化CRF, crf, (short)i降噪强度, out str多线程编码库);
        }

        //        public static string Get_视频编码库(VideoInfo info, out string str视流格式, out string str多线程编码库, out float adjust_crf, out string preset, out string denoise) {
        //            preset = string.Empty;
        //            denoise = string.Empty;


        //            if (b根据帧率自动强化CRF) {
        //                if (str视频编码库 == "libvvenc") {
        //                    if (info.f输出帧率 > 210) adjust_crf = crf + 6;
        //                    else if (info.f输出帧率 > 170) adjust_crf = crf + 5;
        //                    else if (info.f输出帧率 > 115) adjust_crf = crf + 4;
        //                    else if (info.f输出帧率 > 57) adjust_crf = crf + 3;
        //                    else if (info.f输出帧率 > 40) adjust_crf = crf + 2;
        //                    else if (info.f输出帧率 > 28) adjust_crf = crf + 1;
        //                    else {
        //                        adjust_crf = crf;
        //                    }
        //                } else {
        //                    if (info.f输出帧率 > 210) adjust_crf = crf + 9;
        //                    else if (info.f输出帧率 > 170) adjust_crf = crf + 8;
        //                    else if (info.f输出帧率 > 115) adjust_crf = crf + 7;
        //                    else if (info.f输出帧率 > 57) adjust_crf = crf + 5;
        //                    else if (info.f输出帧率 > 40) adjust_crf = crf + 3;
        //                    else if (info.f输出帧率 > 28) adjust_crf = crf + 1;
        //                    else {
        //                        adjust_crf = crf;
        //                    }
        //                }
        //            } else {
        //                adjust_crf = crf;
        //            }

        //            if (adjust_crf < 6) adjust_crf = 6;
        //            else if (adjust_crf > 61) adjust_crf = 61; //实测CRF62、63有概率无法转码。

        //            preset = speed.ToString( );

        //            if (str视频编码库 == "libsvtav1") {
        //                str视流格式 = "av1";
        //                if (speed == 0) preset = "-1";
        //                else if (speed > 7) preset = "10";
        //                // Encoder preset, presets < 0 are for debugging. Higher presets means faster encodes, but with a quality tradeoff, default is 10 [-1-10]

        //                //vmaf接近时，p3比p5高2个CRF。
        //                if (b根据帧率自动强化CRF) {//presert 3 ,crf 35 做基准档位参考
        //                    adjust_crf -= speed - 3;//每个压缩档位相差约1CRF
        //                }

        //                if (adjust_crf < 0) adjust_crf = 0;
        //                else if (adjust_crf > 63) adjust_crf = 63;

        //                string cmd = $" -pix_fmt yuv420p10le -c:v libsvtav1 -crf {adjust_crf}  -preset {preset} -svtav1-params tune=0:lp=1";//:pin=1 

        //                str多线程编码库 = $" -pix_fmt yuv420p10le -c:v libsvtav1 -crf {adjust_crf}  -preset {preset} -svtav1-params tune=0";

        //                /*
        //                 * --tune Specifies whether to use PSNR or VQ as the tuning metric [0 = VQ, 1 = PSNR, 2 = SSIM]
        //                 * SVT-AV1 Tune PSNR（默认）在高动态场景下的画质得分最好，但综合上不高
        //                 * 无论画面类型，只要静态画面偏多，Tune VQ 就相比 Tune PSNR 有优势，否则 Tune PSNR 最好
        //                https://iavoe.github.io/av1-web-tutorial/HTML/index.html 测试结论

        //                string cmd = $" -pix_fmt yuv420p10le -c:v libsvtav1 -crf {adjust_crf}  -preset {preset} -svtav1-params tune=0:lp=1:pin=1";
        //                str多线程编码库 = $" -pix_fmt yuv420p10le -c:v libsvtav1 -crf {adjust_crf}  -preset {preset} -svtav1-params tune=0";
        //                /*

        //--lp                         Target (best effort) number of logical cores to be used. 0 means all. Refer to Appendix A.1 of the user guide, default is 0 [0, core count of the machine]
        //--pin                        Pin the execution to the first --lp cores. Overwritten to 1 when `--ss` is set. Refer to Appendix A.1 of the user guide, default is 0 [0-1]
        //--ss                         Specifies which socket to run on, assumes a max of two sockets. Refer to Appendix A.1 of the user guide, default is -1 [-1, 0, -1]
        //                */

        //                if (b磨皮降噪 && i降噪强度 > 0) {
        //                    cmd += ":film-grain=" + i降噪强度;
        //                    str多线程编码库 += ":film-grain=" + i降噪强度;
        //                    denoise = ".dn" + i降噪强度;
        //                }
        //                return cmd;
        //            } else if (str视频编码库 == "libvvenc") {
        //                str视流格式 = "vvc";
        //                if (speed == 4) {
        //                    preset = "medium";
        //                } else {
        //                    if (b根据帧率自动强化CRF) {
        //                        if (speed == 3) {
        //                            preset = "slow"; adjust_crf += 2;
        //                        } else if (speed == 5) {
        //                            preset = "fast"; adjust_crf -= 1;
        //                        } else if (speed >= 0 & speed < 3) {
        //                            preset = "slower"; adjust_crf += 4;
        //                        } else if (speed > 5) {
        //                            preset = "faster"; adjust_crf -= 2;
        //                        } else {
        //                            preset = "medium";
        //                        }
        //                    } else {
        //                        if (speed == 3) {
        //                            preset = "slow";
        //                        } else if (speed == 5) {
        //                            preset = "fast";
        //                        } else if (speed >= 0 & speed < 3) {
        //                            preset = "slower";
        //                        } else if (speed > 5) {
        //                            preset = "faster";
        //                        } else {
        //                            preset = "medium";
        //                        }
        //                    }
        //                }

        //                if (adjust_crf < 0) adjust_crf = 0;
        //                else if (adjust_crf > 63) adjust_crf = 63;

        //                //--MCTF [2]`                      Enable GOP based temporal filter. (0:off, 1:filter all frames, 2:use SCC detection to disable for screen coded content)
        //                //启用基于 GOP 的时域滤波 (MCTF) (0:关闭, 1:滤波所有帧, 2:使用 SCC 检测在屏幕内容编码时禁用)*

        //                string cmd = $" -pix_fmt yuv420p10le -c:v libvvenc -qp {adjust_crf} -preset {preset} -vvenc-params  threads=1:MaxParallelFrames=1:WaveFrontSynchro=0";

        //                str多线程编码库 = $" -pix_fmt yuv420p10le -c:v libvvenc -qp {adjust_crf} -preset {preset}";

        //                if (b磨皮降噪) {
        //                    cmd += ":MCTF=1:MCTFSpeed=0" ;
        //                    str多线程编码库 += " -vvenc-params MCTF=1:MCTFSpeed=0";
        //                    denoise = "";
        //                }

        //                return cmd;
        //            } else {
        //                str视流格式 = "av1";
        //                if (speed > 7) preset = "8";
        //                //--cpu-used=<arg> Speed setting (0..6 in good mode, 5..11 in realtime mode, 0..9 in all intra mode)
        //                //ssim接近时，p3大约比p5、p6=p8 节约4CRF

        //                //cpu-used 0充分压缩，12高压，3~4标压缩、5快压、6~8实时编码，压缩率低
        //                if (b根据帧率自动强化CRF) {//cpu-used 2 crf 32 做基准档位参考
        //                    if (speed == 1) adjust_crf += 1;
        //                    else if (speed == 0) adjust_crf += 2;
        //                    else if (speed == 3) adjust_crf -= 1;
        //                    else if (speed == 4) adjust_crf -= 2;
        //                    else if (speed > 4) adjust_crf -= 4;

        //                    if (!b单线程) adjust_crf -= 1;//aom多线程质量降低1CRF
        //                }

        //                if (adjust_crf < 0) adjust_crf = 0;
        //                else if (adjust_crf > 61) adjust_crf = 61;


        //                string cmd = $" -pix_fmt yuv420p10le -c:v libaom-av1 -crf {adjust_crf} -cpu-used {preset}  -threads 1 -aom-params row-mt=0:fp-mt=0";

        //                str多线程编码库 = $" -pix_fmt yuv420p10le -c:v libaom-av1 -crf {adjust_crf} -cpu-used {preset}";

        //                if (b磨皮降噪) {
        //                    cmd += ":denoise-noise-level=" + i降噪强度;
        //                    str多线程编码库 += " -aom-params denoise-noise-level=" + i降噪强度;
        //                    denoise = ".dn" + i降噪强度;
        //                }
        //                return cmd;
        //            }
        //        }

    }
}
