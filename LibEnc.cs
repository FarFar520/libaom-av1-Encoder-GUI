using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace 破片压缩器 {
    public class LibEnc {

        public static readonly Dictionary<string, 预设> dic显示_VVenC预设 = new Dictionary<string, 预设>( ) {
            //--preset [medium] select preset for specific encoding setting (faster, fast, medium, slow, slower, medium_lowDecEnergy)
                {"slower (最慢)",new 预设(value预设: "slower",crf偏移: 3) },
                {"slow (慢)",new 预设(value预设:"slow",crf偏移:2) },
                {"medium (中速)",new 预设(value预设:"medium",crf偏移: 0) },//编码器默认值
                //{"medium (中速,快速解码）",new 预设(value预设:"medium_lowDecEnergy",crf偏移:0) }, lib库中没有
                {"fast (快)",new 预设(value预设:"fast",crf偏移:-1) },
                {"faster (最快)",new 预设(value预设:"faster",crf偏移:-2) }
        };
        public static readonly Dictionary<string, 预设> dic显示_aomenc预设 = new Dictionary<string, 预设>( ) {
            //--cpu-used=<arg> Speed setting (0..6 in good mode, 5..12 in realtime mode, 0..9 in all intra mode)
                {"0 (最慢)",new 预设(value预设:"0" ,crf偏移: 5) },//编码器默认
                {"1 (慢速三挡↓)",new 预设(value预设:"1",crf偏移: 4) },
                {"2 (慢速二挡↓)",new 预设(value预设:"2",crf偏移: 3) },
                {"3 (慢速一挡↓)",new 预设(value预设:"3",crf偏移: 2) },
                {"4 (慢)",new 预设(value预设:"4",crf偏移: 1) },
                {"5 (中速)",new 预设(value预设:"5",crf偏移: 0) },
                {"6 (快)",new 预设(value预设:"6",crf偏移: -1) },
                {"7 (快速一挡)",new 预设(value预设:"7",crf偏移: -2) },
                {"8 (快速二挡)",new 预设(value预设:"8",crf偏移: -3) },
                //{"9 (快速3)",new 预设(value预设:"9",crf偏移: -4) },lib库中没有
                //{"10 (快速4)",new 预设(value预设:"10",crf偏移: -5) },
                //{"11 (快速5)",new 预设(value预设:"11" ,crf偏移: -6)},
                //{"12 (最快速)",new 预设(value预设:"12" ,crf偏移: -7)}
        };
        public static readonly Dictionary<string, 预设> dic显示_SvtAv1EncApp预设 = new Dictionary<string, 预设>( ) {
            //--preset Encoder preset, presets < 0 are for debugging.Higher presets means faster encodes, but with a quality tradeoff, default is 10[-1 - 13]
                {"-1 (最慢,完全体)",new 预设(value预设:"-1" ,crf偏移: 3)},
                {"0 (更慢)",new 预设(value预设:"0" ,crf偏移: 2)},
                {"1 (慢)",new 预设(value预设:"1",crf偏移: 1) },
                {"2 (低速,提画质)",new 预设(value预设:"2" ,crf偏移: 0)},
                {"3 (中速)",new 预设(value预设:"3",crf偏移: -1)},
                {"4 (快速一挡)",new 预设(value预设:"4",crf偏移: -2) },
                {"5 (快速二挡)",new 预设(value预设:"5" ,crf偏移: -3)},
                {"6 (快速三挡)",new 预设(value预设:"6" ,crf偏移: -4)},
                {"7 (快速四挡)",new 预设(value预设:"7" ,crf偏移: -5)},
                {"8 (快速五挡)",new 预设(value预设:"8" ,crf偏移: -6)},
                {"9 (快速六挡)",new 预设(value预设:"9" ,crf偏移: -7)},
                {"10 (快速七挡)",new 预设(value预设:"10" ,crf偏移: -8)},//编码器默认
                {"11 (快速八挡)",new 预设(value预设:"11" ,crf偏移: -9)},
                {"12 (快速九挡)",new 预设(value预设:"12" ,crf偏移: -10)},
                {"13 (最快速)",new 预设(value预设:"13" ,crf偏移: -11)}
        };
        public static readonly Dictionary<string, 预设> dic显示_rav1e预设 = new Dictionary<string, 预设>( ) {
            //--speed <SPEED>  [default: 6]
          //Speed level (0 is best quality, 10 is fastest)
          //Speeds 10 and 0 are extremes and are generally not recommended
                {"0 (最慢)",new 预设(value预设:"0" ,crf偏移: 18) },
                {"1 (慢速三挡↓)",new 预设(value预设:"1",crf偏移: 15) },
                {"2 (慢速二挡↓)",new 预设(value预设:"2",crf偏移: 12) },
                {"3 (慢速一挡↓)",new 预设(value预设:"3",crf偏移: 9) },
                {"4 (慢)",new 预设(value预设:"4",crf偏移: 6) },
                {"5 (低速)",new 预设(value预设:"5",crf偏移: 3) },
                {"6 (中速)",new 预设(value预设:"6",crf偏移: 0) },//编码器默认
                {"7 (快速一挡)",new 预设(value预设:"7",crf偏移: -3) },
                {"8 (快速二挡)",new 预设(value预设:"8",crf偏移: -6) },
                {"9 (快速三挡)",new 预设(value预设:"9",crf偏移: -9) },
                {"10 (快速四挡)",new 预设(value预设:"10",crf偏移: -12) },
        };
        public static readonly Dictionary<string, 预设> dic显示_x265预设 = new Dictionary<string, 预设>( ) {
            //--preset <string>  Trade off performance for compression efficiency. Default medium,
               //ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow, or placebo
                {"placebo (最慢,安慰剂)",new 预设(value预设:"placebo" ,crf偏移: 4,min_判定帧型:9)},
                {"VerySlow (特慢)",new 预设(value预设:"veryslow" ,crf偏移: 3,min_判定帧型:9){eFPS_2K=0.3f,eFPS_4K=0.05f }},
                {"slower (慢+)",new 预设(value预设:"slower",crf偏移: 2,min_判定帧型:5)},
                {"slow (慢)",new 预设(value预设:"slow" ,crf偏移: 1,min_判定帧型:5)},
                {"medium (中速)",new 预设(value预设:"medium",crf偏移: 0,min_判定帧型:5){eFPS_2K=3,eFPS_4K=1 }},//编码器默认值
                {"fast (快)",new 预设(value预设:"fast",crf偏移: -0.5f,min_判定帧型:5) },
                {"faster (快+)",new 预设(value预设:"faster" ,crf偏移: -1,min_判定帧型:5)},
                {"veryfast (特快)",new 预设(value预设:"veryfast" ,crf偏移: -1.5f, min_判定帧型 : 5)},
                {"superfast (特快+)",new 预设(value预设:"superfast" ,crf偏移: -2,min_判定帧型:4)},
                {"ultrafast (最快)",new 预设(value预设:"ultrafast" ,crf偏移: -2.5f,min_判定帧型:4){eFPS_2K=5,eFPS_4K=2 }}
        };
        public static readonly Dictionary<string, 预设> dic显示_x264预设 = new Dictionary<string, 预设>( ) {
            //--preset <string>  Trade off performance for compression efficiency. Default medium,
               //ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow, or placebo
                {"medium (中速)",new 预设(value预设:"medium",crf偏移: 0,min_判定帧型:5){eFPS_2K=13,eFPS_4K=4.6f } },//编码器默认值    
                {"placebo (最慢,安慰剂)",new 预设(value预设:"placebo" ,crf偏移: 4,min_判定帧型:9)},
                {"VerySlow (特慢)",new 预设(value预设:"veryslow" ,crf偏移: 3,min_判定帧型:9)},
                {"slower (慢+)",new 预设(value预设:"slower",crf偏移: 2,min_判定帧型:5)},
                {"slow (慢)",new 预设(value预设:"slow" ,crf偏移: 1,min_判定帧型:5)},
                //{"medium (中速)",new 预设(value预设:"medium",crf偏移: 0,min_判定帧型:5)},//编码器默认值
                //{"fast (快)",new 预设(value预设:"fast",crf偏移: -0.5f,min_判定帧型:5) },
                //{"faster (快+)",new 预设(value预设:"faster" ,crf偏移: -1,min_判定帧型:5)},
                //{"veryfast (特快)",new 预设(value预设:"veryfast" ,crf偏移: -1.5f, min_判定帧型 : 5)},
                //{"superfast (特快+)",new 预设(value预设:"superfast" ,crf偏移: -2,min_判定帧型:4)},
                {"ultrafast (最快)",new 预设(value预设:"ultrafast" ,crf偏移: -2.5f,min_判定帧型:4){eFPS_2K=10,eFPS_4K=2 }}
        };


        public static Dictionary<string, LibEnc> dic_编码库_初始设置 = new Dictionary<string, LibEnc>( );

        public static void fx编码库初始化( ) {
            add_libvvenc( );
            add_libaom_av1( );
            add_libsvtav1( );
            add_librav1e( );
            add_libx265( );
            add_libx264( );
        }
        static void add_libvvenc( ) {
            LibEnc libEnc = new LibEnc(code: "vvc", value编码库: "libvvenc", key预设: "-preset", key编码器传参: "-vvenc-params"
                , CRF参数: new Num参数(key: "-qp", "qpa", range_min: 0, range_max: 63, def: 32, i小数位: 0, my_min: 16, my_max: 35, my_value: 28)
                , b多线程优先: true, value内参单线程: "MaxParallelFrames=1:IFPLines=0:IFP=0:WaveFrontSynchro=0", value外参单线程: "-threads 1", i默认线程数: 5);
            /*
             * --MTProfile [off] set automatic multi-threading setting (-1: auto, 0: off, 1,2,3: on, enables tiles, IFP and WPP automatically depending on the number of threads)
             * 设置自动多线程设置 (-1: 自动, 0: 关闭, 1,2,3: 开启，根据线程数自动启用 Tile, IFP 和 WPP)
             
             * --MaxParallelFrames [-1] Maximum number of frames to be processed in parallel(0:off, >=2: enable parallel frames)
             * 并行处理的最大帧数(0:关闭, >=2: 启用并行帧处理)
            
             * --IFPLines [-1] Inter-Frame Parallelization(IFP) explicit CTU-lines synchronization offset (-1: default mode with two lines, 0: off)
             * 帧间并行化 (IFP) 显式 CTU 行同步偏移 (-1: 默认模式带两行偏移, 0: 关闭)
             
             * --IFP [auto] Inter-Frame Parallelization(IFP) (-1: auto, 0: off, 1: on, with default setting of IFPLines)
             * 帧间并行化 (IFP) (-1: 自动, 0: 关闭, 1: 开启，使用 IFPLines 的默认设置)
             
             *--WaveFrontSynchro [auto]`        Enable entropy coding sync (WPP) (-1: auto, 0: off, 1: on)
             *启用熵编码同步 (WPP) (-1: 自动, 0: 关闭, 1: 开启)*
             */
            libEnc.Set使用位深(12);

            libEnc.Set固定内参(new string[] { "PerceptQPA=1" });
            //-qpa, --PerceptQPA [0] Enable perceptually motivated QP adaptation, XPSNR based (0:off, 1:on)
            //启用基于感知的 QP 自适应，基于 XPSNR(0:关闭, 1:开启)

            libEnc.Noise去除参数 = new SHORT内参带显示(key: "MCTF=1:MCTFSpeed={0}", str最小提示: "质量最佳", str最大提示: "速度最快", str摘要: ".mctf", b默启: true, min: 0, max: 4, use: 0);
            libEnc.GOP跃秒 = new SHORT内参(key: "RefreshSec={0}", min: 1, max: short.MaxValue, def: 1);
            libEnc.GOP跃帧 = new INT内参(key: "IntraPeriod={0}", min: 1, max: int.MaxValue, def: 0);
            libEnc._arr帧率CRF偏移 = new short[,] { { 210, 8 }, { 170, 7 }, { 115, 6 }, { 88, 5 }, { 58, 4 }, { 48, 3 }, { 38, 2 }, { 28, 1 } };

            libEnc.Add所有预设(dic显示_VVenC预设);

            libEnc.str画质参考 = "vvenc画质范围参考↓\r\n蓝光原盘：QPA=13\r\n视觉无损：QPA=18\r\n超清：\tQPA=23\r\n高清：\tQPA=28（推荐）\r\n标清：\tQPA=32（默认）";

            dic_编码库_初始设置.Add("高压缩 vvc @VVenC", libEnc);
        }
        static void add_libaom_av1( ) {
            LibEnc libEnc = new LibEnc(code: "av1", value编码库: "libaom-av1", key预设: "-cpu-used", key编码器传参: "-aom-params"
                , CRF参数: new Num参数(key: "-crf", "crf", range_min: 0, range_max: 63, def: 32, i小数位: 0, my_min: 8, my_max: 40, my_value: 28)
                , b多线程优先: false, value内参单线程: "row-mt=0:fp-mt=0", value外参单线程: "-threads 1", i默认线程数: 3);

            //libEnc.Add所有预设("2 (慢速↓2)", dic显示_aomenc预设);
            libEnc.Add所有预设(dic显示_aomenc预设);
            libEnc.Noise去除参数 = new SHORT内参带显示(key: "denoise-noise-level={0}:enable-dnl-denoising=1", str最小提示: "微微一降", str最大提示: "最大降噪", str摘要: ".dn", b默启: false, min: 1, max: 50, use: 4);

            libEnc._arr帧率CRF偏移 = new short[,] { { 210, 9 }, { 170, 8 }, { 115, 7 }, { 57, 5 }, { 40, 3 }, { 28, 1 } };

            //int i视觉无损 = 23, i轻损 = 28, i忍损 = 35;//aomenc,crf固定，cpu-used不同，质量区别无法肉眼察觉，速度、体积可观测。
            libEnc.str画质参考 = "aomenc画质范围参考↓\r\n蓝光原盘：CRF=8\r\n视觉无损：CRF=16\r\n超清：\tCRF=23\r\n高清：\tCRF=28（推荐）\r\n标清：\tCRF=32（默认）";

            dic_编码库_初始设置.Add("中压缩 av1 @aomenc", libEnc);
        }
        static void add_libsvtav1( ) {
            LibEnc libEnc = new LibEnc(code: "av1", value编码库: "libsvtav1", key预设: "-preset", key编码器传参: "-svtav1-params"
                , CRF参数: new Num参数(key: "-crf", "crf", range_min: 0, range_max: 63, def: 35, i小数位: 0, my_min: 8, my_max: 43, my_value: 30)
                , b多线程优先: true, value内参单线程: "lp=1", value外参单线程: string.Empty, i默认线程数: 16);


            libEnc.Add所有预设("2", dic显示_SvtAv1EncApp预设);
            libEnc.Set固定内参(new string[] { "tune=0" });//"scd=1"

            libEnc.Noise去除参数 = new SHORT内参带显示(key: "film-grain={0}:film-grain-denoise=1", str最小提示: "微微一降", str最大提示: "最大降噪", str摘要: ".dn", b默启: false, min: 1, max: 50, use: 4);
            /*
            --film - grain < 整数 0~50，默认 0：关，被--film - grain - denoise覆盖 > 开启 FGS 并指定噪点分离强度，涉及编码前的噪声的分离与建模、解码端的再合成。强度取决于画面类型，例如录像片源适合中~高强度、动画与录屏片源适合低强度（见 SVT-AV1 Common question）——1：最弱分离、50：最强分离。
            0（关闭）：跳过 FGS，完全使用时域降噪过滤（TF）分离，编码和降噪噪声画面
            
            --film - grain - denoise < 0 / 1，默认 0：关，覆盖--film - grain > 丢弃 FGS 分离的噪点，--film - grain 的分离强度间接转化为降噪强度。
            */

            libEnc._arr帧率CRF偏移 = new short[,] { { 210, 9 }, { 170, 8 }, { 115, 7 }, { 57, 5 }, { 40, 3 }, { 28, 1 } };

            libEnc.GOP跃秒 = new SHORT内参(key: "keyint={0}s", 1, short.MaxValue, def: 5);

            //int i视觉无损 = 23, i轻损 = 28, i忍损 = 35;
            libEnc.str画质参考 = "SVT-AV1画质范围参考↓\r\n蓝光原盘：CRF=8\r\n视觉无损：CRF=18\r\n超清：\tCRF=25\r\n高清：\tCRF=30（推荐）\r\n标清：\tCRF=35（默认）";

            dic_编码库_初始设置.Add("多线程 av1 @svtav1", libEnc);
        }
        static void add_librav1e( ) {
            LibEnc libEnc = new LibEnc(code: "av1", value编码库: "librav1e", key预设: "-speed", key编码器传参: "-rav1e-params"
                , CRF参数: new Num参数(key: "-qp", "qp", range_min: 0, range_max: 255, def: 100, i小数位: 0, my_min: 8, my_max: 180, my_value: 100)
                , b多线程优先: false, value内参单线程: "", value外参单线程: "-threads 1", i默认线程数: 1);

            //--scd-speed <SCD_SPEED>
            //Speed level for scene - change detection, 0: best quality, 1: fastest mode. [default: 0 for s0 - s9, 1 for s10]

            libEnc.Add所有预设(dic显示_rav1e预设);
            libEnc.Set固定内参(new string[] { "scd-speed=0" });

            libEnc.Noise去除参数 = new SHORT内参带显示(key: "photon-noise={0}", str最小提示: "微微一降", str最大提示: "最大降噪", str摘要: ".dn", b默启: false, min: 1, max: 64, use: 4);
            //Uses grain synthesis to add photon noise to the resulting encode. Takes a strength value 0-64

            libEnc._arr帧率CRF偏移 = new short[,] { { 210, 9 }, { 170, 8 }, { 115, 7 }, { 57, 5 }, { 40, 3 }, { 28, 1 } };

            //int i视觉无损 = 23, i轻损 = 28, i忍损 = 35;//aomenc,crf固定，cpu-used不同，质量区别无法肉眼察觉，速度、体积可观测。
            libEnc.str画质参考 = "rav1e画质范围参考↓\r\n蓝光原盘：QP=32\r\n视觉无损：QP=64\r\n超清：QP=93\r\n高清：QP=100（推荐）\r\n标清：\tCRF=128";

            dic_编码库_初始设置.Add("中压缩 av1 @rav1e", libEnc);
        }
        static void add_libx265( ) {
            string str内参单线程 = "pools=none:frame-threads=1:lookahead-threads=1:no-wpp=1:lookahead-slices=0"; //rc-lookahead最小值=b帧+1
            LibEnc libEnc = new LibEnc(code: "hevc", value编码库: "libx265", key预设: "-preset", key编码器传参: "-x265-params"
                , CRF参数: new Num参数(key: "-crf", "crf", range_min: 0, range_max: 51, def: 28, i小数位: 1, my_min: 8, my_max: 30, my_value: 23.5f)
                , b多线程优先: false, value内参单线程: str内参单线程, value外参单线程: "-threads 1", i默认线程数: 16);


            libEnc.Add所有预设("veryslow", dic显示_x265预设);
            libEnc.Set固定内参(new string[] { "fades=1", "no-info=1", "hist-scenecut=1", "tune=ssim" });//"single-sei=1" x265 [warning]: None of the SEI messages are enabled. Disabling Single SEI NAL

            libEnc.lookahead = new BYTE内参("rc-lookahead={0}", 3, 250, 20);

            libEnc.Noise去除参数 = new SHORT内参带显示(key: "nr-intra={0}:nr-inter={0}:mcstf=1", str最小提示: "微微一降", str最大提示: "最大降噪", str摘要: ".dn", b默启: false, min: 1, max: 2000, use: 64);

            libEnc._arr帧率CRF偏移 = new short[,] { { 210, 5 }, { 170, 4 }, { 115, 3 }, { 57, 2 }, { 40, 1 } };

            libEnc.str画质参考 = "x265画质范围参考↓\r\n超清：CRF=18\r\n高清：CRF=23（推荐）\r\n标清：CRF=28（默认）";
            dic_编码库_初始设置.Add("高画质 hevc @x265", libEnc);
        }
        static void add_libx264( ) {
            LibEnc libEnc = new LibEnc(code: "avc", value编码库: "libx264", key预设: "-preset", key编码器传参: "-x264-params"
                , CRF参数: new Num参数(key: "-crf", "crf", range_min: 0, range_max: 51, def: 28, i小数位: 1, my_min: 0, my_max: 30, my_value: 23.5f)
                , b多线程优先: false, value内参单线程: "lookahead-threads=1:sliced-threads=1", value外参单线程: "-threads 1", i默认线程数: 16);


            libEnc.Add所有预设("meidum", dic显示_x264预设);

            libEnc.Set使用位深(0);
            //libEnc.Set固定内参(new string[] { "tune=psnr" });

            //libEnc.Noise去除参数 = new SHORT内参带显示(key: "nr={0}", str最小提示: "微微一降", str最大提示: "最大降噪", str摘要: ".dn", b默启: false, min: 1, max: 64, use: 4);

            libEnc._arr帧率CRF偏移 = new short[,] { { 210, 3 }, { 115, 2 }, { 57, 1 } };

            libEnc.str画质参考 = "x264画质范围参考↓\r\n蓝光原盘：CRF=10\r\n视觉无损：CRF=18\r\n超清：CRF=21\r\n高清：CRF=23（推荐）\r\n标清：CRF=23（默认）";
            dic_编码库_初始设置.Add("轻快压 avc @x264", libEnc);
        }


        public class 预设 {
            float _crf偏移;
            string _value;
            byte _min_判定帧型 = 0;

            public float eFPS_2K = 0.1f, eFPS_4K = 0.03f;

            public 预设(string value预设, float crf偏移) {
                _value = value预设; _crf偏移 = crf偏移;
            }
            public 预设(string value预设, float crf偏移, byte min_判定帧型) {
                _value = value预设; _crf偏移 = crf偏移; _min_判定帧型 = min_判定帧型;
            }

            public float get_CRF(bool b微调crf, float crf, Num参数 CRF) {
                if (b微调crf) {
                    crf += _crf偏移;
                }
                if (crf < CRF.range_min) crf = CRF.range_min;
                else if (crf > CRF.range_max) crf = CRF.range_max;

                return crf;
            }
            public float get_CRF(bool b微调crf, float crf, Num参数 CRF, float fps, short[,] fps偏移) {
                if (b微调crf) {
                    crf += _crf偏移;
                    int len = fps偏移.GetLength(0);
                    for (int i = 0; i < len; i++) {
                        if (fps >= fps偏移[i, 0]) {
                            crf += fps偏移[i, 1];
                            break;
                        }
                    }
                }
                if (crf < CRF.range_min) crf = CRF.range_min;
                else if (crf > CRF.range_max) crf = CRF.range_max;

                return crf;
            }

            public string value => _value;
            public byte min_判定帧型 => _min_判定帧型;
        }
        public class Num参数 {
            string _key, _name;
            float _def;
            float _my_value, _set_value;

            short _range_min, _range_max, _my_min, _my_max;

            byte _i小数位;

            public Num参数(string key, string name, short range_min, short range_max, float def, byte i小数位
                , short my_min, short my_max, float my_value) {
                _key = key; _name = name; _range_min = range_min; _range_max = range_max; _def = def; _i小数位 = i小数位;
                _my_min = my_min; _my_max = my_max; _set_value = _my_value = my_value;
            }

            public void setValue(float value) {
                _set_value = value;
            }

            public float set_value => _set_value;

            public string key => _key;
            public string name => _name;

            public short range_min => _range_min;
            public short range_max => _range_max;
            public short my_min => _my_min;
            public short my_max => _my_max;

            public float def => _def;
            public byte i小数位 => _i小数位;

            public float my_value => _my_value;

        }

        public class INT内参 {
            string _key;
            public int _min, _max, _def;
            public INT内参(string key, int min, int max, int def) {
                _key = key; _min = min; _max = max; _def = def;
            }
            public string get(int value) {
                if (value == _def) return string.Empty;
                else if (value < _min) return string.Format(_key, _min);
                else if (value > _max) return string.Format(_key, _max);
                else return string.Format(_key, value);
            }
        }
        public class BYTE内参 {
            string _key;
            public byte _min, _max, _def;
            public BYTE内参(string key, byte min, byte max, byte def) {
                _key = key; _min = min; _max = max; _def = def;
            }
            public string get(byte value) {
                if (value == _def) return string.Empty;
                if (value < _min) return string.Format(_key, _min);
                else if (value > _max) return string.Format(_key, _max);
                else return string.Format(_key, value);
            }
        }

        public class SHORT内参 {
            string _key;
            public short _min, _max, _def;
            public SHORT内参(string key, short min, short max, short def) {
                _key = key; _min = min; _max = max; _def = def;
            }
            public string get(short value) {
                if (value == _def) return string.Empty;
                else if (value < _min) return string.Format(_key, _min);
                else if (value > _max) return string.Format(_key, _max);
                else return string.Format(_key, value);
            }
        }

        public class SHORT内参带显示 {
            string _key;
            short _min, _max, _use;
            string _str最小提示, _str最大提示, _str摘要;
            bool _b默启;
            public SHORT内参带显示(string key, string str最小提示, string str最大提示, string str摘要, bool b默启, short min, short max, short use) {
                _str最小提示 = str最小提示; _str最大提示 = str最大提示;
                _str摘要 = str摘要;
                _b默启 = b默启;
                _key = key; _min = min; _max = max; _use = use;
            }

            public string get提示参数(int value) {
                if (value >= max)
                    return _str最大提示 + string.Format(_key, value);
                else if (value <= min)
                    return _str最小提示 + string.Format(_key, value);
                else
                    return string.Format(_key, value);
            }
            public string get参数(int value) {
                return string.Format(_key, value);
            }

            public string get参数(int value, out string str摘要) {
                str摘要 = _str摘要 + value;
                return string.Format(_key, value);
            }

            public string key => _key;
            public short min => _min;
            public short max => _max;
            public short def => _use;
            public bool b默启 => _b默启;
        }

        byte _byte位深 = 10;
        string _code;
        bool _b多线程优先;
        ushort _i默认线程数;
        short[,] _arr帧率CRF偏移;
        string _key显示预设, _key预设, _key编码器传参;

        string _value编码库, _value内参单线程, _value外参单线程;

        string[] _arr固定内参;

        public LibEnc(string code, string value编码库, string key预设, string key编码器传参
            , Num参数 CRF参数
            , bool b多线程优先, string value内参单线程, string value外参单线程, ushort i默认线程数) {

            _code = code;
            _value编码库 = value编码库;
            _key预设 = key预设;
            _key编码器传参 = key编码器传参;

            this.CRF参数 = CRF参数;

            _b多线程优先 = b多线程优先;
            _value内参单线程 = value内参单线程;
            _value外参单线程 = value外参单线程;

            _i默认线程数 = i默认线程数;
        }


        Dictionary<string, 预设> _dic_选择_预设 = new Dictionary<string, 预设>( );
        static Regex rege多空格 = new Regex(" {2,}", RegexOptions.Compiled);

        Dictionary<byte, string> dic_位深_限缩参数 = new Dictionary<byte, string>( ) { { 0, "" }, { 8, "yuv420p" }, { 10, "yuv420p10le" }, { 12, "yuv420p12le" }, { 14, "yuv420p14le" }, { 16, "yuv420p16le" } };

        public Num参数 CRF参数 = null;
        public INT内参 GOP跃帧 = null;
        public SHORT内参 GOP跃秒 = null;
        public BYTE内参 lookahead = null;
        public SHORT内参带显示 Noise去除参数 = null;

        public bool b多线程优先 => _b多线程优先;
        public ushort i默认线程数 => _i默认线程数;

        public string str画质参考;

        public string code => _code;

        public string key预设 => _key预设;
        public string key显示预设 => _key显示预设;
        public string key编码器传参 => _key编码器传参;

        public string value编码库 => _value编码库;

        public Dictionary<string, 预设> dic_选择_预设 => _dic_选择_预设;

        void Set使用位深(byte byte位深) {
            _byte位深 = byte位深;
        }
        void Set固定内参(string[] arr固定内参) {
            _arr固定内参 = arr固定内参;
        }
        void Add所有预设(Dictionary<string, 预设> dic_选择_预设) {
            _dic_选择_预设 = dic_选择_预设;

            foreach (string key in dic_选择_预设.Keys) {
                if (key.Contains("中")) {
                    _key显示预设 = key;
                    break;
                }
            }
        }

        void Add所有预设(string key显示预设, Dictionary<string, 预设> dic_选择_预设) {
            _dic_选择_预设 = dic_选择_预设;
            foreach (var kv in dic_选择_预设) {
                if (kv.Value.value == key显示预设) {
                    _key显示预设 = kv.Key;
                    return;
                }
            }
            string str中速 = string.Empty;
            foreach (string key in dic_选择_预设.Keys) {
                if (key.Contains("中")) {
                    str中速 = key;
                }
                if (key.Contains(key显示预设)) {
                    _key显示预设 = key显示预设;
                    break;
                }
            }
            if (string.IsNullOrEmpty(_key显示预设)) _key显示预设 = str中速;

        }

        public string get参数_编码器预设画质(string key选择预设, bool b微调CRF, decimal crf) {
            if (!dic_选择_预设.TryGetValue(key选择预设, out 预设 enc预设)) {
                enc预设 = dic_选择_预设[_key显示预设];
            }
            return string.Format("-c:v {0} {1} {2} {3} {4}", _value编码库, _key预设, enc预设.value, CRF参数.key, enc预设.get_CRF(b微调CRF, (float)crf, CRF参数));
        }

        public string get参数_编码器预设画质(string key选择预设, bool b多线程, bool b微调CRF, decimal crf) {
            if (!dic_选择_预设.TryGetValue(key选择预设, out 预设 enc预设)) {
                enc预设 = dic_选择_预设[_key显示预设];
            }

            if (b多线程 && !_b多线程优先) crf++;

            List<string> list传递内参 = new List<string>( );

            float adjust_crf = enc预设.get_CRF(b微调CRF, (float)crf, CRF参数);

            if (!b多线程 && !string.IsNullOrEmpty(_value内参单线程))
                list传递内参.Add(_value内参单线程);

            string str视编参数;

            if (list传递内参.Count > 0) {
                if (!b多线程)
                    str视编参数 = string.Format("-c:v {0} {1} {2} {3} {4} {5} {6} {7}", _value编码库, _value外参单线程, _key预设, enc预设.value, CRF参数.key, adjust_crf, key编码器传参, list传递内参[0]);
                else
                    str视编参数 = string.Format("-c:v {0} {1} {2} {3} {4}", _value编码库, _key预设, enc预设.value, CRF参数.key, adjust_crf);

                for (int i = 1; i < list传递内参.Count; i++) {
                    str视编参数 += ":" + list传递内参[i];
                }
            } else {
                if (!b多线程)
                    str视编参数 = string.Format("-c:v {0} {1} {2} {3} {4} {5}", _value编码库, _value外参单线程, _key预设, enc预设.value, CRF参数.key, adjust_crf);
                else
                    str视编参数 = string.Format("-c:v {0} {1} {2} {3} {4}", _value编码库, _key预设, enc预设.value, CRF参数.key, adjust_crf);

            }
            return rege多空格.Replace(str视编参数, " ");
        }

        public string get参数_编码器预设画质(string key选择预设, decimal crf, bool b内降噪, int value降噪) {
            if (!dic_选择_预设.TryGetValue(key选择预设, out 预设 enc预设)) {
                enc预设 = dic_选择_预设[_key显示预设];
            }
            if (b内降噪) {
                return string.Format("-c:v {0} {1} {2} {3} {4} {5} {6}", _value编码库, _key预设, enc预设.value, CRF参数.key, crf, key编码器传参, Noise去除参数.get参数(value降噪));
            } else {
                return string.Format("-c:v {0} {1} {2} {3} {4}", _value编码库, _key预设, enc预设.value, CRF参数.key, crf);
            }
        }

        public string get参数_编码器预设画质(string key选择预设, bool b多线程, bool b微调CRF, decimal crf, bool b内降噪, int value降噪) {
            if (!dic_选择_预设.TryGetValue(key选择预设, out 预设 enc预设)) {
                enc预设 = dic_选择_预设[_key显示预设];
            }
            if (b多线程 && !_b多线程优先) crf++;

            List<string> list传递内参 = new List<string>( );

            if (_arr固定内参 != null) {
                list传递内参.AddRange(_arr固定内参);
            }

            float adjust_crf = enc预设.get_CRF(b微调CRF, (float)crf, CRF参数);

            if (!b多线程 && !string.IsNullOrEmpty(_value内参单线程))
                list传递内参.Add(_value内参单线程);

            if (b内降噪 && Noise去除参数 != null)
                list传递内参.Add(Noise去除参数.get参数(value降噪));

            string str视编参数;

            if (list传递内参.Count > 0) {
                if (!b多线程)
                    str视编参数 = string.Format("-c:v {0} {1} {2} {3} {4} {5} {6} {7}", _value编码库, _value外参单线程, _key预设, enc预设.value, CRF参数.key, adjust_crf, key编码器传参, list传递内参[0]);
                else
                    str视编参数 = string.Format("-c:v {0} {1} {2} {3} {4} {5} {6}", _value编码库, _key预设, enc预设.value, CRF参数.key, adjust_crf, key编码器传参, list传递内参[0]);

                for (int i = 1; i < list传递内参.Count; i++) {
                    str视编参数 += ":" + list传递内参[i];
                }
            } else {
                if (!b多线程)
                    str视编参数 = string.Format("-c:v {0} {1} {2} {3} {4} {5}", _value编码库, _value外参单线程, _key预设, enc预设.value, CRF参数.key
                        , enc预设.get_CRF(b微调CRF, (float)crf, CRF参数));
                else
                    str视编参数 = string.Format("-c:v {0} {1} {2} {3} {4}", _value编码库, _key预设, enc预设.value, CRF参数.key, adjust_crf);

            }
            return rege多空格.Replace(str视编参数, " ");
        }

        public string get压视频参数(VideoInfo info, List<string> list传递内参, string key选择预设, bool b多线程, bool b内降噪, bool b微调CRF, float crf, short value降噪
            , out string str多线程编码库, out string str最低画质编码库, out string str多线程最低画质编码库) {
            if (!dic_选择_预设.TryGetValue(key选择预设, out 预设 enc预设)) {
                enc预设 = dic_选择_预设[_key显示预设];
            }

            if (b多线程 && enc预设.eFPS_4K * 转码队列.i逻辑核心数 > info.f输入帧率) {//(假定单线程能实时解码）简单判断编码帧率是否超过解码速度
                info.IN.ffmpeg单线程解码 = string.Empty;
            }

            info.OUT.enc = value编码库;
            info.OUT.str视流格式 = code;
            info.OUT.preset = enc预设.value;
            info.OUT.str量化名 = CRF参数.name;

            if (b微调CRF && !b多线程 && !_b多线程优先) crf++;

            info.OUT.adjust_crf = enc预设.get_CRF(b微调CRF, crf, CRF参数, info.f输出帧率, _arr帧率CRF偏移);

            if (_arr固定内参 != null) list传递内参.AddRange(_arr固定内参);

            if (b内降噪 && Noise去除参数 != null) list传递内参.Add(Noise去除参数.get参数(value降噪, out info.OUT.denoise));


            if (!b多线程) {
                if (lookahead != null) {
                    if (enc预设.min_判定帧型 > 0)
                        list传递内参.Add(lookahead.get(enc预设.min_判定帧型));
                }
            }

            string str限缩位深格式化 = _byte位深 == 0 ? string.Empty : ("-pix_fmt " + dic_位深_限缩参数[_byte位深]);

            string str视编参数 = string.Format(" {0} -c:v {1} {2} {3} {4} {5} {6} ", str限缩位深格式化, _value编码库, _value外参单线程, _key预设, enc预设.value, CRF参数.key, info.OUT.adjust_crf);

            str多线程编码库 = string.Format(" {0} -c:v {1} {2} {3} {4} {5} ", str限缩位深格式化, _value编码库, _key预设, enc预设.value, CRF参数.key, info.OUT.adjust_crf);

            str最低画质编码库 = string.Format(" {0} -c:v {1} {2} {3} {4} {5} {6} ", str限缩位深格式化, _value编码库, _value外参单线程, _key预设, enc预设.value, CRF参数.key, CRF参数.range_max);
            str多线程最低画质编码库 = string.Format(" {0} -c:v {1} {2} {3} {4} {5} ", str限缩位深格式化, _value编码库, _key预设, enc预设.value, CRF参数.key, CRF参数.range_max);



            if (list传递内参.Count > 0 || GOP跃秒 != null || GOP跃帧 != null) {
                string str内参 = key编码器传参;

                if (list传递内参.Count > 0) {
                    str内参 += ' ' + list传递内参[0];
                    for (int i = 1; i < list传递内参.Count; i++) str内参 += ":" + list传递内参[i];
                }

                str最低画质编码库 += str内参;
                str多线程最低画质编码库 += str内参;

                string strGOP = string.Empty;
                if (GOP跃秒 != null) {
                    str内参 += ':' + GOP跃秒.get((short)Settings.sec_gop);
                    strGOP += ':' + GOP跃秒.get((short)info.time视频时长.TotalSeconds);
                } else if (GOP跃帧 != null) {
                    str内参 += ':' + GOP跃帧.get(Settings.sec_gop);
                    strGOP += ':' + GOP跃帧.get(info.outSumFrames);
                }

                str视编参数 += str内参;
                str多线程编码库 += str内参;
                str最低画质编码库 += strGOP;
                str多线程最低画质编码库 += strGOP;

                if (!string.IsNullOrEmpty(_value内参单线程)) {
                    str视编参数 += ":" + _value内参单线程;
                    str最低画质编码库 += ":" + _value内参单线程;
                }
            } else if (!string.IsNullOrEmpty(_value内参单线程)) {
                str最低画质编码库 += string.Format(" {0} {1}", key编码器传参, _value内参单线程);
                str视编参数 += string.Format(" {0} {1}", key编码器传参, _value内参单线程);
            }

            str多线程编码库 = rege多空格.Replace(str多线程编码库, " ");
            return rege多空格.Replace(str视编参数, " ");
        }
    }
}
