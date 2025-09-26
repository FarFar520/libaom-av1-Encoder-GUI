using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace 破片压缩器 {
    public partial class Form破片压缩: Form {

        public static HashSet<string> mapVideoExt = new HashSet<string>( ) { ".y4m", ".265", ".x265", ".h265", ".hevc", ".264", ".h264", ".x264", ".avi", ".wmv", ".wmp", ".wm", ".asf", ".mpg", ".mpeg", ".mpe", ".m1v", ".m2v", ".mpv2", ".mp2v", ".ts", ".tp", ".tpr", ".trp", ".vob", ".ifo", ".ogm", ".ogv", ".mp4", ".m4v", ".m4p", ".m4b", ".3gp", ".3gpp", ".3g2", ".3gp2", ".mkv", ".rm", ".ram", ".rmvb", ".rpm", ".flv", ".mov", ".qt", ".nsv", ".dpg", ".m2ts", ".m2t", ".mts", ".dvr-ms", ".k3g", ".skm", ".evo", ".nsr", ".amv", ".divx", ".wtv", ".f4v", ".mxf" };

        public static bool b保存异常日志 = true;

        int i切片间隔秒 = 60;
        decimal d检测镜头精度 = new decimal(0.1);

        bool b更改过输入路径 = true, b更改过缓存路径 = true, b需要重扫输入 = false, b需要重扫缓存 = false, b最小化 = false;

        int NumberOfProcessors = 0, NumberOfCores = 0, NumberOfLogicalProcessors = 0;
        float f保底缓存切片 = 8;
        string str最后一条信息 = string.Empty;
        string str正在源文件夹 = string.Empty;
        string str正在转码文件夹 = "D:\\破片转码";
        string txt等待转码视频文件夹 = string.Empty;

        public const string str切片根目录 = "D:\\破片转码";


        public static AutoResetEvent
            autoReset切片 = new AutoResetEvent(false),
            autoReset转码 = new AutoResetEvent(false),
            autoReset协转 = new AutoResetEvent(false),
            autoReset合并 = new AutoResetEvent(false),
            autoReset初始信息 = new AutoResetEvent(false);

        List<DirectoryInfo> list输入路径 = new List<DirectoryInfo>( );
        List<DirectoryInfo> list缓存路径 = new List<DirectoryInfo>( );

        Thread thread切片, thread转码, thread合并, thread编码节点, thread初始信息;

        object obj转码队列 = new object( ), obj合并队列 = new object( );


        Video_Roadmap video正在转码文件 = null, video热乎的切片 = null;
        HashSet<string> map已切片文件小写路径 = new HashSet<string>( );
        List<Video_Roadmap> list_等待转码队列 = new List<Video_Roadmap>( );

        Dictionary<string, Video_Roadmap> dic_完成路径_等待合并 = new Dictionary<string, Video_Roadmap>( );

        public Form破片压缩( ) {
            InitializeComponent( );

            LibEnc.fx编码库初始化( );
            comboBox_lib.Items.AddRange(LibEnc.dic_编码库_初始设置.Keys.ToArray( ));

            thread切片 = new Thread(fn后台切片);
            thread切片.IsBackground = true;
            thread切片.Name = "切片";

            thread转码 = new Thread(fn后台转码);
            thread转码.IsBackground = true;
            thread转码.Name = "转码";

            thread合并 = new Thread(fn后台合并);
            thread合并.IsBackground = true;
            thread合并.Name = "合并";

            thread编码节点 = new Thread(fn协同视频编码);
            thread编码节点.IsBackground = true;
            thread编码节点.Name = "协转";
        }

        void add日志(string log) {
            str最后一条信息 = log;
            log = $"{DateTime.Now:yy-MM-dd HH:mm:ss} {log}";
            if (listBox日志.InvokeRequired) {
                listBox日志.Invoke(new Action(( ) => {
                    listBox日志.Items.Add(log);
                    listBox日志.SelectedIndex = listBox日志.Items.Count - 1;
                }));
            } else {
                listBox日志.Items.Add(log);
                listBox日志.SelectedIndex = listBox日志.Items.Count - 1;
            }

        }

        void txt日志(string log) {
            if (textBox日志.InvokeRequired) {
                textBox日志.Invoke(new Action(( ) => textBox日志.Text = log));
            } else {
                textBox日志.Text = log;
            }
        }

        bool is有效视频(FileInfo file) {
            if (mapVideoExt.Contains(file.Extension.ToLower( )) && File.Exists(file.FullName)) {
                string name = file.Name.ToLower( );
                if (name.Contains("svtav1") || name.Contains("aomav1") || name.Contains("vvenc")) return false;

                if (map已切片文件小写路径.Add(file.FullName.ToLower( ))) {
                    return true;
                }
            }

            return false;
        }

        bool is缓存低 {
            get {
                i剩余缓存 = 转码队列.i并发任务数;
                if (video热乎的切片 != null) i剩余缓存 += video热乎的切片.i剩余切片数量;
                if (video正在转码文件 != null) i剩余缓存 += video正在转码文件.i剩余切片数量;
                for (int i = 0; i < list_等待转码队列.Count; i++) i剩余缓存 += list_等待转码队列[i].i剩余切片数量;
                转码队列.i切片缓存 = i剩余缓存;
                return i剩余缓存 < f保底缓存切片;
            }
        }

        void fn后台切片( ) {
            while (true) {
                更改过文件夹: b更改过输入路径 = b需要重扫输入 = false;
                lock (obj转码队列) { list_等待转码队列.Clear( ); }
                DirectoryInfo[] arrDir = list输入路径.ToArray( );
                foreach (DirectoryInfo dir in arrDir) {
                    FileInfo[] arrFileInfo;
                    if (File.Exists(dir.FullName))
                        arrFileInfo = new FileInfo[] { new FileInfo(dir.FullName) };//如果是文件，直接处理。
                    else if (!Directory.Exists(dir.FullName))
                        continue;//此处循环存在等待时长，文件夹有被移动风险。判断一次文件夹存在情况。
                    else {
                        add日志($"查找视频:{dir.FullName}");
                        arrFileInfo = dir.GetFiles( );
                    }
                    foreach (FileInfo file in arrFileInfo) {
                        if (is有效视频(file)) {
                            str正在转码文件夹 = $"{str切片根目录}\\{file.DirectoryName.Replace(file.Directory.Root.FullName, "").Trim('\\')}";
                            Video_Roadmap video = new Video_Roadmap(file, str正在转码文件夹, Settings.b无缓转码);

                            if (!video.b解码60帧判断交错(out StringBuilder builder)) //扫描60帧，出结果较快。
                                video.b读取视频头(out builder);

                            if (video.b无缓转码) {//有无缓转码.info文件时，代表未完成任务为无缓模式
                                video.b查找MKA音轨( );
                                Task.Run(( ) => fn无缓参数(video));
                                Thread.Sleep(999); //最快每秒开启一次扫描任务。
                                while (list_等待转码队列.Count > 2 || 转码队列.list扫分段.Count > 1) autoReset切片.WaitOne( );//事不过三，储备3个等待转码队列暂停扫描。
                            } else {
                                if (!video.b查找MKA音轨( )) {
                                    add日志($"提取音轨：{video.strMKA文件名}");
                                    Task.Run(( ) => { video.b提取MKA音轨(ref builder); });//体积大的视频会等好几分钟
                                    if (!转码队列.b有任务) autoReset初始信息.Set( );
                                }//mkvmerge小概率返回结果后，内存中的数据未完全写入磁盘。已增加命令行 --flush-on-close 完整写入磁盘退出

                                if (!video.b有切片记录) {//如果找不到现有切片，先进行切片。
                                    if (Settings.b扫描场景) {
                                        string log = "扫描关键帧差异，决定切片场景：" + file.Name;
                                        add日志(log);
                                        if (!转码队列.b有任务) autoReset初始信息.Set( );//扫描场景有点费时间，增加进度输出。
                                        if (video.b检测场景切换(d检测镜头精度, ref builder)) {//扫描关键帧需要占用大量CPU时间，任务时间片和转码可以复用。转码中可以再开一条线程，ffmpeg单线程扫描视频，提高CPU利用率和现实时间复用率。
                                            log = $"以关键帧差异切片：{file.Name}";
                                            add日志(log);
                                            video.Fx按场景切片并获取列表(ref builder);
                                        }
                                    }
                                    if (!video.b有切片记录) {//按转场切片有失败的可能性，重新切一次。
                                        string log = $"按{i切片间隔秒}秒切片：{file.Name}";
                                        add日志(log);
                                        //当前的工作流程设计是等到切片成功才开始转码。第一个视频初始化尚有优化空间。
                                        //如处理单个视频体积高达1TB，在8盘RAID0读写平均500MB/s 也需要1024*1024/512/60=34.133333分钟后才开始转码。
                                        //第一个开始切片的视频提高初始化效率的逻辑：每切出一块，开始转码一块。第二个视频则不需要。
                                        if (!转码队列.b有任务) autoReset初始信息.Set( );//切片大文件有点费时间，增加进度输出。
                                        video.Fx按时间切片并获取列表(i切片间隔秒, ref builder);//当视频体积非常大时，切片耗时较长，软件完全看不出进度
                                    }
                                }

                                if (video.b有切片记录) {
                                    video热乎的切片 = video;
                                    int i剩余切片数量 = video.i剩余切片数量;
                                    if (i剩余切片数量 > 0) add日志($"恭喜！获得视频碎片{i剩余切片数量}片 @ {file.FullName}");
                                    Task.Run(( ) => { fn协编参数(video); });//生成节点参数。
                                    txt日志(builder.ToString( ));
                                    fx定时刷新切片数量(10);//协同转码会取走缓存，每10分钟刷新一次
                                } else {
                                    add日志("切片异常：" + file.Name);
                                }

                            }

                            try { Thread.Sleep(999); } catch { }
                        }
                        if (b更改过输入路径) goto 更改过文件夹;//中途更改文件夹优先级高，立刻跳出重新扫描
                    }
                }
                add日志($"输入目录已全部扫描！");
                while (!b需要重扫输入) autoReset切片.WaitOne( );
            }
        }//1号线程，准备好了切片，后续线程才能顺序调度。

        void fn协编参数(Video_Roadmap roadmap) {
            if (Settings.b自动裁黑边) {
                add日志($"扫描黑边：{roadmap.fi输入视频.Name}");
                roadmap.b扫描视频黑边生成剪裁参数( );
                if (!转码队列.b有任务) autoReset初始信息.Set( );
            }
            if (roadmap.b拼接转码摘要( ))//多文件时，外部节点依赖存储机生成任务配置.ini
                roadmap.fx保存任务配置( );

            roadmap.fx清理存编终止切片( );

            lock (obj转码队列) { list_等待转码队列.Add(roadmap); }
            video热乎的切片 = null;
            autoReset转码.Set( );

            string lowPath = roadmap.di编码成功.FullName.ToLower( );
            if (!dic_完成路径_等待合并.ContainsKey(lowPath)) {
                lock (obj合并队列) {
                    dic_完成路径_等待合并.Add(lowPath, roadmap);
                }
                if (roadmap.watcher编码成功文件夹 != null)//协编任务采用成功文件夹监控方式
                    roadmap.watcher编码成功文件夹.Created += 新增成功视频检查合并;
            }
        }
        void fn无缓参数(Video_Roadmap roadmap) {
            if (Settings.b自动裁黑边) {
                add日志($"扫描黑边：{roadmap.fi输入视频.Name}");
                roadmap.b扫描视频黑边生成剪裁参数( );
                if (!转码队列.b有任务) autoReset初始信息.Set( );
            }
            if (roadmap.b拼接转码摘要( ))
                roadmap.fx清理存编终止切片( );//多文件时，外部节点依赖存储机生成任务配置.ini

            roadmap.vTimeBase.b读取无缓转码csv(roadmap.di编码成功, roadmap.info.time视频时长);


            if (roadmap.vTimeBase.i总分段 < 1) {
                while (转码队列.list扫分段.Count > 1) Thread.Sleep(999);//避免同时启动
                add日志($"开始扫描视频帧数据：{roadmap.fi输入视频.Name}");
                if (!Settings.b扫描场景) roadmap.vTimeBase.Start按关键帧(Settings.sec_gop);
                else roadmap.vTimeBase.Start按转场(转码队列.b缓存余量充足, (float)d检测镜头精度, Settings.sec_gop, Settings.i分割GOP, 0.25f);
                if (!转码队列.b有任务) autoReset初始信息.Set( );
            } else {
                add日志($"已读取无缓转码.csv {roadmap.vTimeBase.i总分段} 段：{roadmap.fi输入视频.Name}");
            }

            if (roadmap.is无缓视频未完成) {
                lock (obj转码队列) {
                    list_等待转码队列.Add(roadmap);
                }//由转码线程结束后加入合并队列
                autoReset转码.Set( );
            } else {//未完成的暂不加入合并队列，减少合并线程重复判断
                string lowPath = roadmap.di编码成功.FullName.ToLower( );
                if (!dic_完成路径_等待合并.ContainsKey(lowPath)) {
                    lock (obj合并队列) {
                        dic_完成路径_等待合并.Add(lowPath, roadmap);
                    }
                    autoReset合并.Set( );
                }
            }
            Thread.Sleep(999);
            autoReset切片.Set( );
        }

        void fn后台转码( ) {
            while (true) {
                while (list_等待转码队列.Count < 1) {
                    if (thread切片.ThreadState == (ThreadState.Background | ThreadState.WaitSleepJoin))
                        autoReset切片.Set( );//当转码队列为空间隔触发扫描任务
                    autoReset转码.WaitOne(3333);
                }
                while (list_等待转码队列.Count > 0) {
                    fx设置输出目录为当前时间( );
                    Video_Roadmap videoTemp;
                    lock (obj转码队列) {
                        if (list_等待转码队列.Count > 0) {
                            videoTemp = list_等待转码队列[0];
                            list_等待转码队列.RemoveAt(0);
                        } else break;
                    }
                    video正在转码文件 = videoTemp;
                    str正在源文件夹 = video正在转码文件.str输入路径;

                    this.Invoke(new Action(( ) => timer刷新编码输出.Start( )));//要委托UI线程启动计时器才能正确启动。

                    if (video正在转码文件.b后台转码MKA音轨( )) {//单独转码OPUS音轨，CPU资源占用少，放在视频队列之前。
                        add日志($"转码音轨：{video正在转码文件.strMKA路径}");
                    }

                    while (转码队列.i多进程数量 == 0) {
                        autoReset转码.WaitOne( );
                    }//存储机设置为0任务时，无限等待，编码交给外部算力节点。

                    if (videoTemp.b无缓转码) {
                        if (File.Exists(videoTemp.fi输入视频.FullName)) {//任务有足够时间间隔，检测一次源文件存在情况，当手动删除源文件时跳过任务。
                            while (video正在转码文件.b转码下一个分段(out External_Process external_Process)) {
                                add日志($"开始转码：{external_Process.fi编码.FullName}");
                                转码队列.ffmpeg等待入队(external_Process);//有队列上限
                            }

                            if (!dic_完成路径_等待合并.ContainsKey(videoTemp.lower完整路径_输入视频)) {
                                lock (obj合并队列) {
                                    dic_完成路径_等待合并.Add(videoTemp.lower完整路径_输入视频, videoTemp);
                                }
                            }
                        }
                    } else {
                        while (video正在转码文件.b协同切片尝试回调( )) {
                            while (video正在转码文件.b转码下一个切片(out External_Process external_Process)) {
                                add日志($"开始转码：{external_Process.fi源.FullName}");
                                转码队列.ffmpeg等待入队(external_Process);//有队列上限
                                if (is缓存低) autoReset切片.Set( );
                            }
                        }
                    }
                    video正在转码文件 = null;
                }

                if (dic_完成路径_等待合并.Count > 0) {
                    add日志("切片皆加入转码任务，等待合并中。加入新视频需点刷新按钮！");
                    autoReset合并.Set( );
                }
            }

        }//2号线程，一个目录下转码完成，调度3号线程。

        void fn协同视频编码( ) {//0号线程想设计为局域网多分机读取切片，转未处理的不同碎片，转完汇入主机合并。
                          //2。通过尝试移动碎片各自工作文件夹，分机自主处理各自任务，存在任务碎片化加剧问题。等待合并过程拉长。

            if (list缓存路径.Count == 0) autoReset协转.WaitOne( );
            thread编码节点.Priority = ThreadPriority.Highest;//协同转码线程优先级高，保证能及时响应新任务。

            HashSet<string> set协编过的配置 = new HashSet<string>( );
            while (true) {
                更改过文件夹: b更改过缓存路径 = false;
                DirectoryInfo[] arrDir = list缓存路径.ToArray( );
                foreach (DirectoryInfo dir in arrDir) {
                    FileInfo[] arrFI = dir.GetFiles("任务配置.ini", SearchOption.AllDirectories);
                    foreach (FileInfo fi in arrFI) {
                        if (set协编过的配置.Add(fi.FullName.ToLower( ))) {//一个切片目录，只尝查找一次协编码文件。
                            Encoding_Node node = new Encoding_Node(fi);
                            if (node.b准备协同任务(out string tips)) {
                                str正在转码文件夹 = node.di切片文件夹.FullName;
                                do {
                                    while (node.b转码下一个切片(out External_Process external_Process)) {
                                        add日志($"开始转码：{external_Process.fi编码.FullName}");
                                        转码队列.ffmpeg等待入队(external_Process);//有队列上限
                                    }
                                } while (node.b未处理切片加入队列( ));
                            } else {
                                if (!string.IsNullOrEmpty(tips))
                                    add日志(tips);
                            }
                        }
                        if (b更改过缓存路径) goto 更改过文件夹;//中途更改文件夹优先级高，立刻跳出重新扫描
                    }
                }
                autoReset协转.WaitOne(60000);//暂未考虑HTTP通信方案，使用每分钟定时检查一次存储文件。
            }
        }

        void fn后台合并( ) {
            StringBuilder sb合并 = new StringBuilder( );
            HashSet<string> set已合并文件夹 = new HashSet<string>( );
            while (true) {
                while (dic_完成路径_等待合并.Count < 1) autoReset合并.WaitOne( );//等到
                Video_Roadmap[] arr等待合并队列 = dic_完成路径_等待合并.Values.ToArray( );
                for (int i = 0; i < arr等待合并队列.Length; i++) {
                    if (arr等待合并队列[i].b音轨需要更新) {
                        add日志($"转码音轨：{arr等待合并队列[i].str切片路径}");
                        arr等待合并队列[i].b更新OPUS音轨( );//音轨设置可以在视频转码过程中更改，即刻生效。
                    }
                    if (arr等待合并队列[i].b文件夹下还有切片) {
                        continue;
                    } else if (set已合并文件夹.Add(arr等待合并队列[i].di编码成功.FullName.ToLower( ))) {//只尝试合并一次。
                        add日志($"开始合并：{arr等待合并队列[i].str切片路径}");
                        string str合并结果;
                        if (arr等待合并队列[i].b转码后混流( )) {//混流任务属于磁盘读写任务，理论上会和解流任务抢占资源。
                            str合并结果 = $"合并成功：{arr等待合并队列[i].str切片路径}\\{arr等待合并队列[i].str输出文件名}";
                        } else {
                            str合并结果 = $"合并失败！{arr等待合并队列[i].str切片路径}";
                        }

                        add日志(str合并结果);
                        sb合并.AppendLine(str合并结果);

                        if (arr等待合并队列[i].watcher编码成功文件夹 != null)
                            arr等待合并队列[i].watcher编码成功文件夹.Created -= 新增成功视频检查合并;

                        arr等待合并队列[i].fx删除协编文件( );

                        lock (obj合并队列) {
                            dic_完成路径_等待合并.Remove(arr等待合并队列[i].di编码成功.FullName.ToLower( ));//合并后尝试移除，减少循环对音频文件判断，切片任务还有添加回来的可能性。
                        }
                    }
                }
                if (!b需要重扫输入 && dic_完成路径_等待合并.Count == 0 && !转码队列.b有任务) {
                    timer刷新编码输出.Stop( );
                    转码队列.Has汇总输出信息(out string str编码信息);
                    txt日志($"{str编码信息}\r\n\r\n目录下视频已完成，增加新视频点击刷新按钮\r\n\r\n{sb合并.ToString( )}");
                }
                autoReset合并.WaitOne( );//合并等待,
            }
        }//3号线程，任务收尾工作。

        void fn初始信息( ) {
            while (true) {
                autoReset初始信息.WaitOne( );
                Thread.Sleep(3333);
                this.Invoke(new Action(( ) => timer刷新编码输出.Stop( )));
                while (转码队列.Get独立进程输出(out string info)) {
                    if (转码队列.b有任务 && 转码队列.Has汇总输出信息(out string str编码速度)) {
                        info += "\r\n\r\n" + str编码速度;
                    }
                    if (info.Length > 0) {
                        textBox日志.Invoke(new Action(( ) => {
                            textBox日志.Text = info;
                            textBox日志.SelectionStart = textBox日志.TextLength - 1;
                            textBox日志.ScrollToCaret( );
                        }));
                    }
                    autoReset初始信息.WaitOne(3333);
                }
                this.Invoke(new Action(( ) => timer刷新编码输出.Start( )));
            }
        }

        bool b手动刷新切片数量 = false;
        int i剩余缓存 = 0;
        void fx定时刷新切片数量(int i分钟) {
            刷新缓存:
            int i剩余缓存 = 0;
            if (video热乎的切片 != null) i剩余缓存 += video热乎的切片.i剩余切片数量;
            if (video正在转码文件 != null) i剩余缓存 += video正在转码文件.i剩余切片数量;
            for (int i = 0; i < list_等待转码队列.Count; i++) i剩余缓存 += list_等待转码队列[i].i剩余切片数量;
            if (i剩余缓存 > f保底缓存切片) {
                if (b手动刷新切片数量) {
                    b手动刷新切片数量 = false;//手动刷新显示日志，自动刷新不显示
                    add日志($"切片数量充足：{转码队列.i并发任务数} / {i剩余缓存}");
                }
                autoReset切片.WaitOne(i分钟 * 60000);//引入协同转码功能后，改为10分钟刷新一次

                if (i剩余缓存 > f保底缓存切片) goto 刷新缓存;//成功文件夹增加文件后，缓存数量扣减。
            }
            转码队列.i切片缓存 = i剩余缓存;
            add日志($"切片缓存：{转码队列.i并发任务数} / {i剩余缓存}，查找下一视频……");
        }

        void CPUNum( ) {
            try { 硬件.计算机名 = "主机：" + Environment.MachineName + " "; } catch { }

            try {
                foreach (var item in new ManagementObjectSearcher("Select * from Win32_ComputerSystem").Get( )) {
                    try { NumberOfProcessors += int.Parse(item["NumberOfProcessors"].ToString( )); } catch { }
                    try { NumberOfLogicalProcessors += int.Parse(item["NumberOfLogicalProcessors"].ToString( )); } catch { }
                    try {
                        if (double.TryParse(item["TotalPhysicalMemory"].ToString( ), out double memory)) {
                            硬件.内存大小 = "内存：" + Math.Round(memory / 1024 / 1024 / 1024) + "GB\r\n";
                        }
                    } catch { }
                }
            } catch { }
            try {
                List<string> listCPU = new List<string>( );
                foreach (var item in new ManagementObjectSearcher("Select * from Win32_Processor").Get( )) {
                    NumberOfCores += int.Parse(item["NumberOfCores"].ToString( ));
                    Encoding_Node.cpuId = item["ProcessorId"].ToString( );
                    string name = item["Name"].ToString( ).Trim( ) + " ";
                    if (!name.Contains("AMD") && !name.Contains("Intel")) {
                        name = item["Manufacturer"].ToString( ) + " " + name;
                    }
                    listCPU.Add(name + "@" + item["CurrentClockSpeed"].ToString( ) + "MHz "
                          + item["Version"].ToString( ).Trim( ) + " " + item["Processorid"].ToString( )); // 返回CPU名称，可能包含型号信息
                }

                for (int i = 1; i < listCPU.Count; i++)
                    硬件.CPU名 += "\r\n" + listCPU[i];

                listCPU.ForEach(item => 硬件.CPU名 += item + "\r\n");

                if (NumberOfProcessors > 1)
                    硬件.多路CPU = NumberOfProcessors + "路\r\n";

            } catch { }

            硬件.str摘要 = 硬件.计算机名 + 硬件.内存大小 + 硬件.多路CPU + 硬件.CPU名 + "\r\n";

            foreach (ManagementObject mo in new ManagementClass("Win32_BaseBoard").GetInstances( )) {
                Encoding_Node.SerialNumber = mo.Properties["SerialNumber"].Value.ToString( );
                break;
            }
            转码队列.i物理核心数 = NumberOfCores;
            转码队列.i逻辑核心数 = NumberOfLogicalProcessors;
            add日志($"( {Encoding_Node.str主机名称} ) {NumberOfProcessors}处理器 {NumberOfCores}核心 {NumberOfLogicalProcessors}线程 [{Encoding_Node.cpuId}] {Encoding_Node.SerialNumber}");


            if (NumberOfLogicalProcessors <= 64) {//超过64核心，长整数溢出
                转码队列.arr_单核指针 = new IntPtr[NumberOfLogicalProcessors];
                long core = 1;
                int c = 0;
                for (; c < NumberOfLogicalProcessors; c++) {//优先从靠前核心调用。
                    转码队列.arr_单核指针[c] = (IntPtr)core;
                    core <<= 1;//超过64核心会，长整型溢出，需要系统支持跨组调度。
                }

                List<int> list_T0 = new List<int>( ), list_T1 = new List<int>( ), list_Cores = new List<int>( );

                for (int i = 0; i < NumberOfLogicalProcessors; i += 2) list_T0.Add(i);
                for (int i = (NumberOfLogicalProcessors - NumberOfLogicalProcessors % 2 - 1); i > 0; i -= 2) list_T1.Add(i);

                int a = 0;
                int b = list_T0.Count / 2;
                while (list_T0.Count > 0) {
                    int core_a = list_T0[a];

                    list_Cores.Add(core_a);

                    if (a != b) {
                        list_Cores.Add(list_T0[b]);
                        list_T0.RemoveAt(b);
                    }
                    list_T0.Remove(core_a);

                    b /= 2;
                    a = list_T0.Count - b - 1;

                    if (b == 0) {
                        b = list_T0.Count / 2;
                        a = list_T0.Count - b - 1;
                    }
                    if (a < 0) a = 0;
                }

                a = 0; b = list_T1.Count / 2;
                while (list_T1.Count > 0) {
                    int core_a = list_T1[a];

                    list_Cores.Add(core_a);

                    if (a != b) {
                        list_Cores.Add(list_T1[b]);
                        list_T1.RemoveAt(b);
                    }
                    list_T1.Remove(core_a);

                    b /= 2;
                    a = list_T1.Count - b - 1;

                    if (b == 0) {
                        b = list_T1.Count / 2;
                        a = list_T1.Count - b - 1;
                    }
                    if (a < 0) a = 0;
                }

                转码队列.arr_核心号调度排序 = list_Cores.ToArray( );
                /* 主动管理核心调度，算法思路：每次二分，取两端，让任务尽可能分散于不相邻芯片、逻辑核，降低热点集中概率。
                /* 例如物理Core分布如下 
                 * [ 内核0 ]三缓[ 内核4 ]
                 * [ 内核1 ]三缓[ 内核5 ]
                 * [ 内核2 ]三缓[ 内核6 ]
                 * [ 内核3 ]三缓[ 内核7 ]
                 * 则理想进程队列绑定压榨顺序 [0]、[7]、[4]、[2]、[5|6]、[1|3]
                 */
            }

            numericUpDown_Workers.Maximum = NumberOfLogicalProcessors + 1;
        }

        bool fx文件夹( ) {
            string txt = textBox等待转码视频文件夹.Text.Trim( );
            if (txt等待转码视频文件夹 != txt) {
                txt等待转码视频文件夹 = txt;

                StringBuilder builder有效文件 = new StringBuilder( );

                string[] arrPath = txt.Split('\n');
                HashSet<string> set小写输入路径 = new HashSet<string>( ), set小写缓存路径 = new HashSet<string>( );
                List<DirectoryInfo> list_Temp_输入路径 = new List<DirectoryInfo>( ), list_Temp_缓存路径 = new List<DirectoryInfo>( );
                string str机名大写 = Environment.MachineName.ToUpper( ) + "\\";
                for (int i = 0; i < arrPath.Length; i++) {
                    string path = arrPath[i].Trim( ).TrimEnd('\\').TrimEnd('/');
                    if (path.Length > 3) {// E:\1
                        DirectoryInfo di;
                        try { di = new DirectoryInfo(path); } catch { continue; }
                        if (di.Exists) {
                            string path大写 = di.FullName.TrimStart('\\').TrimStart('/').ToUpper( );
                            if (path大写.StartsWith("D:\\破片转码\\")) continue;//排除程序设计输出路径。

                            if ((di.FullName + "\\").Contains("\\破片转码\\")) {
                                if (path大写.StartsWith(str机名大写)) continue;//排除存储机的网络路径

                                if (set小写缓存路径.Add(di.FullName.ToLower( ))) {
                                    list_Temp_缓存路径.Add(di);
                                    builder有效文件.AppendLine(di.FullName).AppendLine( );
                                }
                            } else if (di.Root.FullName != di.FullName) {
                                if (set小写输入路径.Add(di.FullName.ToLower( ))) {
                                    list_Temp_输入路径.Add(di);
                                    builder有效文件.AppendLine(di.FullName).AppendLine( );
                                }
                            }
                        } else if (File.Exists(di.FullName)) {
                            if (set小写输入路径.Add(di.FullName.ToLower( ))) {
                                list_Temp_输入路径.Add(di);
                                builder有效文件.AppendLine(di.FullName).AppendLine( );
                            }
                        }
                    }
                }

                if (list_Temp_输入路径.Count != list输入路径.Count) {
                    list输入路径 = list_Temp_输入路径;
                    b更改过输入路径 = true;//扫描优先级高，扫完一个文件就判断一次，重扫指定顺序
                } else {
                    for (int i = 0; i < list_Temp_输入路径.Count; i++) {
                        if (list_Temp_输入路径[i].FullName != list输入路径[i].FullName) {//此处包含扫描路径读取顺序更改
                            list输入路径 = list_Temp_输入路径;
                            b更改过输入路径 = true;
                            break;
                        }
                    }
                }

                if (list_Temp_缓存路径.Count != list缓存路径.Count) {
                    list缓存路径 = list_Temp_缓存路径;
                    b更改过缓存路径 = true;
                } else {
                    for (int i = 0; i < list_Temp_缓存路径.Count; i++) {
                        if (list_Temp_缓存路径[i].FullName != list缓存路径[i].FullName) {//此处包含扫描路径读取顺序更改
                            list缓存路径 = list_Temp_缓存路径;
                            b更改过缓存路径 = true;
                            break;
                        }
                    }
                }
                if (!string.Equals(txt, "E:\\Videos", StringComparison.OrdinalIgnoreCase)) {
                    try { File.WriteAllText("破片转码文件夹.txt", builder有效文件.ToString( )); } catch { }
                }
            }

            b需要重扫输入 = list输入路径.Count > 0;
            b需要重扫缓存 = list缓存路径.Count > 0;
            //重扫优先级低，当点过刷新按钮，判断一下有输入文件夹就重新扫描一遍。


            return list输入路径.Count > 0 || list缓存路径.Count > 0; //路径读取成功返回真

        }

        void fx刷新设置( ) {
            b手动刷新切片数量 = true;

            d检测镜头精度 = numericUpDown检测镜头.Value;
            f保底缓存切片 = NumberOfLogicalProcessors * 3 + 1;

            转码队列.i多进程数量 = (int)numericUpDown_Workers.Value;
            numericUpDown_Workers.ForeColor = Color.Black;

            Settings.str选择预设 = comboBox预设.Text;

            Settings.b多线程 = checkBox多线程.Checked;
            Settings.b磨皮降噪 = checkBox_磨皮.Checked;
            Settings.i降噪强度 = trackBar_降噪量.Value;

            Settings.b自定义滤镜 = checkBox_lavfi.Checked;
            Settings.str自定义滤镜 = textBox_lavfi.Text.Trim( ).Trim(',');

            Settings.b根据帧率自动强化CRF = checkBox_DriftCRF.Checked;
            Settings.b硬字幕 = checkBox_硬字幕.Checked;
            Settings.opus = checkBoxOpus.Checked;
            Settings.b音轨同时切片转码 = checkBoxSplitAudio.Checked;

            Settings.b编码后删除切片 = checkBox编码后删除切片.Checked;
            Settings.b转码成功后删除源视频 = checkBox转码成功后删除源视频.Checked;

            Settings.crf = (float)numericUpDown_CRF.Value;
            Settings.b转可变帧率 = checkBox_VFR.Checked;

            Settings.i音频码率 = (int)numericUpDown_AB.Value;
            Settings.i剪裁后宽 = (int)numericUpDown_Width.Value;
            Settings.i剪裁后高 = (int)numericUpDown_Height.Value;
            Settings.i左裁像素 = (int)numericUpDown_Left.Value;
            Settings.i上裁像素 = (int)numericUpDown_Top.Value;
            Settings.i缩小到高 = (int)numericUpDown_ScaleH.Value;
            Settings.i缩小到宽 = Settings.i长边 = (int)numericUpDown_ScaleW.Value;
            Settings.b长边像素 = label_ScaleW.Text == "长边像素";

            Settings.b右上角文件名_切片序列号水印 = checkBox_drawtext.Checked;

            comboBox_Scale.Text = Settings.str缩小文本;

            Settings.lib已设置 = libEnc选中;

            Settings.sec_gop = (int)numericUpDown_GOP.Value;
            Settings.i分割GOP = Scene.i分割最少秒 = (int)numericUpDown_分割最小秒.Value;
        }

        private void 新增成功视频检查合并(object sender, FileSystemEventArgs e) {
            string lowerFullPath = e.FullPath.Substring(0, e.FullPath.Length - e.Name.Length - 1);
            if (dic_完成路径_等待合并.TryGetValue(lowerFullPath, out Video_Roadmap roadmap)) {
                i剩余缓存--;
                if (!roadmap.b文件夹下还有切片) {
                    autoReset合并.Set( );//交给合并线程处理，亦可再写一合并子线程。
                }
            }
        }

        private void timer刷新编码输出_Tick(object sender, EventArgs e) {//辅助线程，显示编码中ffmpeg输出帧信息。
            if (转码队列.Has汇总输出信息(out string str编码速度)) {
                textBox日志.Text = str编码速度;
            }
        }

        void fx设置输出目录为当前时间( ) {
            if (Directory.Exists(str切片根目录)) {
                DirectoryInfo di = new DirectoryInfo(str切片根目录);
                try { di.CreationTime = DateTime.Now; } catch { }
                try { di.LastAccessTime = DateTime.Now; } catch { }
                try { di.LastWriteTime = DateTime.Now; } catch { }
            }
        }

        private void button刷新_Click(object sender, EventArgs e) {
            textBox日志.Text = string.Empty;
            fx刷新设置( );
            if (fx文件夹( )) {
                if (thread切片.IsAlive && thread转码.IsAlive && thread合并.IsAlive && thread编码节点.IsAlive) {
                    if (list缓存路径.Count > 0) {
                        autoReset协转.Set( );
                    }
                    转码队列.autoReset入队.Set( );

                    if (list输入路径.Count > 0) {
                        autoReset初始信息.Set( );
                        autoReset切片.Set( );
                        autoReset转码.Set( );
                        autoReset合并.Set( );
                    }

                    if (转码队列.Has汇总输出信息(out string str编码速度)) {
                        textBox日志.Text = str编码速度;
                    }
                    timer刷新编码输出.Start( );
                } else {
                    if (Video_Roadmap.b查找可执行文件(out string log, out string txt)) {
                        button刷新.Text = "刷新(&R)";
                        thread切片.Start( );
                        thread转码.Start( );
                        thread合并.Start( );
                        thread编码节点.Start( );

                        timer刷新编码输出.Enabled = true;
                    } else {
                        add日志($"需要在工具同目录放入：" + log);
                        textBox日志.Text = txt;
                    }
                    new UpdateFFmpeg( );
                }
            } else {
                add日志($"右侧文本框输入视频存放文件夹路径！");
            }
        }
        private void linkLabel输出文件夹_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            if (e.Button == MouseButtons.Right && str正在源文件夹.Length > 3) {
                try { System.Diagnostics.Process.Start("explorer", str正在源文件夹); } catch { }
            } else {
                if (!Directory.Exists(str正在转码文件夹))
                    try { Directory.CreateDirectory(str正在转码文件夹); } catch { }
                else {
                    fx设置输出目录为当前时间( );
                }
                try { System.Diagnostics.Process.Start("explorer", str正在转码文件夹); } catch { }
            }
        }

        private void checkBoxOpus_CheckedChanged(object sender, EventArgs e) {
            panel_kBPS.Visible = checkBoxOpus.Checked;
        }

        private void label_AR_MouseClick(object sender, MouseEventArgs e) {
            Settings.i声道 = (Settings.i声道 + 1) % 3;
            if (Settings.i声道 == 1) {
                label_AR.Text = "单声道 K";
                numericUpDown_AB.Value = Settings.i音频码率 / 2;
            } else {
                numericUpDown_AB.Value = Settings.i音频码率;
                if (Settings.i声道 == 2) {
                    label_AR.Text = "立体声 K";

                } else {
                    label_AR.Text = "源声道 K";
                }
            }
        }

        private void checkBox转码成功后删除源视频_CheckedChanged(object sender, EventArgs e) {
            Settings.b转码成功后删除源视频 = checkBox转码成功后删除源视频.Checked;
            checkBox转码成功后删除源视频.BackColor = Settings.b转码成功后删除源视频 ? Color.Red : Color.White;
        }

        private void comboBox切片模式_SelectedIndexChanged(object sender, EventArgs e) {
            int SelectedIndex = comboBox切片模式.SelectedIndex;
            Settings.b无缓转码 = false;

            checkBox_硬字幕.Checked = false;
            checkBox_硬字幕.Visible = false;
            checkBox编码后删除切片.Visible = true;

            if (SelectedIndex == 0) {
                i切片间隔秒 = Settings.sec_gop * 6;
                Settings.b扫描场景 = true;
                numericUpDown_分割最小秒.Visible = numericUpDown检测镜头.Visible = true;
            } else if (SelectedIndex == 8) {
                i切片间隔秒 = Settings.sec_gop * 3;
                Settings.b无缓转码 = Settings.b扫描场景 = true;
                checkBox_硬字幕.Visible = true;
                checkBox编码后删除切片.Visible = false;
                numericUpDown_分割最小秒.Visible = numericUpDown检测镜头.Visible = true;

            } else if (SelectedIndex == 9) {
                checkBox_硬字幕.Visible = true;
                checkBox编码后删除切片.Visible = numericUpDown_分割最小秒.Visible = numericUpDown检测镜头.Visible = false;
                i切片间隔秒 = Settings.sec_gop * 6;
                Settings.b无缓转码 = true;
                Settings.b扫描场景 = false;
            } else {
                Settings.b扫描场景 = false;
                numericUpDown_分割最小秒.Visible = numericUpDown检测镜头.Visible = false;
                /*
                ffmpeg扫描转场帧切割
                以间隔5秒左右分割
                以间隔10秒左右分割
                以间隔30秒左右分割
                以间隔1分钟左右分割
                以间隔3分钟左右分割
                以间隔5分钟左右分割
                以间隔10分钟左右分割
                 */
                switch (SelectedIndex) {
                    case 1: i切片间隔秒 = 5; return;
                    case 2: i切片间隔秒 = 10; return;
                    case 3: i切片间隔秒 = 30; return;
                    case 4: i切片间隔秒 = 60; return;
                    case 5: i切片间隔秒 = 180; return;
                    case 6: i切片间隔秒 = 300; return;
                    case 7: i切片间隔秒 = 600; return;
                    default: i切片间隔秒 = Settings.sec_gop; return;
                }
            }
        }

        private void checkBoxSplitAudio_CheckedChanged(object sender, EventArgs e) {
            labelSplitAudio.Visible = checkBoxSplitAudio.Checked;
        }

        private void comboBox_Crop_SelectedIndexChanged(object sender, EventArgs e) {
            int iC = comboBox_Crop.SelectedIndex;
            Settings.b自动裁黑边 = false;
            Settings.b手动剪裁 = true;
            bool show = true;

            numericUpDown_Left.Value = 0;
            numericUpDown_Top.Value = 0;
            if (iC == 1) {
                show = false;
                Settings.b自动裁黑边 = true;
                Settings.b手动剪裁 = false;

            } else if (iC == 2) {
                numericUpDown_Width.Value = 1920;
                numericUpDown_Height.Value = 800;
                //numericUpDown_Top.Value = 140;
            } else if (iC == 3) {
                numericUpDown_Width.Value = 1920;
                numericUpDown_Height.Value = 1032;
                //numericUpDown_Top.Value = 24;
            } else if (iC == 4) {
                numericUpDown_Width.Value = 3840;
                numericUpDown_Height.Value = 1600;
                //numericUpDown_Top.Value = 280;
            } else if (iC == 5) {
                numericUpDown_Width.Value = 3840;
                numericUpDown_Height.Value = 1920;
                //numericUpDown_Top.Value = 120;
            } else if (iC == 6) {
                numericUpDown_Width.Value = 3840;
                numericUpDown_Height.Value = 2024;
                //numericUpDown_Top.Value = 68;
            } else if (iC == 7) {
                numericUpDown_Width.Value = 3840;
                numericUpDown_Height.Value = 2072;
                //numericUpDown_Top.Value = 44;
            } else if (iC == 0) {
                show = false;
                Settings.b手动剪裁 = false;
            }

            panel剪裁.Visible = show;

            //panel_Top.Visible = show;
            //panel_Left.Visible = show;

            //panel_Height.Visible = show;
            //panel_Width.Visible = show;//不可更改显示顺序，界面对齐

        }
        private void comboBox_Scale_SelectedIndexChanged(object sender, EventArgs e) {
            int iC = comboBox_Scale.SelectedIndex;
            numericUpDown_ScaleW.Value = 0;
            numericUpDown_ScaleH.Value = 0;
            if (iC == 0) {
                Settings.b以DAR比例修正 = true;
            } else if (iC == 1) {
                numericUpDown_ScaleW.Value = 960;
            } else if (iC == 2) {
                numericUpDown_ScaleW.Value = 1280;
            } else if (iC == 3) {
                numericUpDown_ScaleW.Value = 1600;
            } else if (iC == 4) {
                numericUpDown_ScaleW.Value = 1920;
            } else if (iC == 5) {
                numericUpDown_ScaleW.Value = 2560;
            } else if (iC == 6) {
                numericUpDown_ScaleW.Value = 3840;
            } else if (iC == 7) {
                Settings.b以DAR比例修正 = false;
            }
        }

        private void label_ScaleW_MouseClick(object sender, MouseEventArgs e) {
            if (label_ScaleW.Text == "输出宽度") {
                label_ScaleW.Text = "长边像素";
                panel输出高.Visible = false;
            } else {
                label_ScaleW.Text = "输出宽度";
                panel输出高.Visible = true;
            }
        }

        private void checkBox_磨皮_CheckedChanged(object sender, EventArgs e) {
            if (checkBox_磨皮.Checked) {
                trackBar_降噪量.Visible = true;
            } else {
                trackBar_降噪量.Visible = false;
                checkBox_磨皮.Text = "磨皮降噪，会大幅降低速度";
            }
        }

        private void Form破片压缩_FormClosing(object sender, FormClosingEventArgs e) {
            if (转码队列.b有任务) {
                e.Cancel = true;//先终止退出
                DialogResult result = MessageBox.Show("是否退出！", "破片转码", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (result == DialogResult.Yes) {
                    e.Cancel = false;
                }
            }
        }

        private void trackBar_降噪量_ValueChanged(object sender, EventArgs e) {
            int n = trackBar_降噪量.Value;
            if (checkBox_磨皮.Checked) {
                checkBox_磨皮.Text = libEnc选中.Noise去除参数.get提示参数(n);
                //checkBox_磨皮.Text = "磨皮降噪×" + n + "（会大幅降低速度）";
            } else {
                checkBox_磨皮.Text = "磨皮降噪，会大幅降低速度";
            }
        }

        private void numericUpDown_GOP_ValueChanged(object sender, EventArgs e) {
            int half = (int)numericUpDown_GOP.Value / 2;
            if (half > numericUpDown_分割最小秒.Value && half <= numericUpDown_分割最小秒.Maximum) {
                numericUpDown_分割最小秒.Value = half;
            }
        }

        private void comboBox预设_DropDownClosed(object sender, EventArgs e) {
            if (video热乎的切片 == null && video正在转码文件 == null)
                add日志(libEnc选中.get参数_编码器预设画质(key选择预设: comboBox预设.Text, b微调CRF: checkBox_DriftCRF.Checked, crf: numericUpDown_CRF.Value));
        }

        decimal crf上次;
        private void numericUpDown_CRF_Click(object sender, EventArgs e) {
            decimal value = numericUpDown_CRF.Value;
            if (value > libEnc选中.CRF参数.range_min && numericUpDown_CRF.Minimum >= value) {
                numericUpDown_CRF.Minimum--;
            }
            if (value < libEnc选中.CRF参数.range_max && numericUpDown_CRF.Maximum <= value) {
                numericUpDown_CRF.Maximum++;
            }
            if (crf上次 != value) {
                crf上次 = value;
                if (video热乎的切片 == null && video正在转码文件 == null)
                    add日志(libEnc选中.get参数_编码器预设画质(key选择预设: comboBox预设.Text, b微调CRF: checkBox_DriftCRF.Checked, crf: numericUpDown_CRF.Value));
            }
        }

        private void checkBox_磨皮_MouseClick(object sender, MouseEventArgs e) {
            add日志(libEnc选中.get参数_编码器预设画质(key选择预设: comboBox预设.Text, crf: numericUpDown_CRF.Value, b内降噪: checkBox_磨皮.Checked, value降噪: trackBar_降噪量.Value));
        }

        private void numericUpDown_CRF_KeyPress(object sender, KeyPressEventArgs e) {
            decimal value = numericUpDown_CRF.Value;
            if (e.KeyChar == 13) {
                if (crf上次 != value) {
                    crf上次 = value;
                    add日志(libEnc选中.get参数_编码器预设画质(key选择预设: comboBox预设.Text, b微调CRF: checkBox_DriftCRF.Checked, crf: numericUpDown_CRF.Value));
                }
            }
        }

        private void numericUpDown_Workers_ValueChanged(object sender, EventArgs e) {
            int workers = (int)numericUpDown_Workers.Value;
            if (转码队列.i多进程数量 > workers && workers > 0) {
                转码队列.i多进程数量 = workers;
                numericUpDown_Workers.ForeColor = Color.Black;
            } else if (thread转码.IsAlive || thread编码节点.IsAlive) {
                numericUpDown_Workers.ForeColor = Color.Red;
            }
        }

        private void numericUpDown_Workers_KeyPress(object sender, KeyPressEventArgs e) {
            if (e.KeyChar == 13 && numericUpDown_Workers.ForeColor == Color.Red) {
                Settings.b多线程 = checkBox多线程.Checked;
                转码队列.i多进程数量 = (int)numericUpDown_Workers.Value;
                numericUpDown_Workers.ForeColor = Color.Black;
                转码队列.autoReset入队.Set( );
            }
        }

        private void 清除ToolStripMenuItem_Click(object sender, EventArgs e) {
            listBox日志.Items.Clear( );
        }

        private void checkBox编码后删除切片_CheckedChanged(object sender, EventArgs e) {
            Settings.b编码后删除切片 = checkBox编码后删除切片.Checked;
        }

        private void textBox等待转码视频文件夹_DragDrop(object sender, DragEventArgs e) {
            string[] arr = e.Data.GetData(DataFormats.FileDrop) as string[];
            string strPath = textBox等待转码视频文件夹.Text.Trim( );
            StringBuilder sb = new StringBuilder( );

            for (int i = 0; i < arr.Length; i++) {
                sb.AppendLine(arr[i]).AppendLine( );
            }

            if (strPath != "E:\\Videos") sb.AppendLine(strPath);
            textBox等待转码视频文件夹.Text = sb.ToString( );
        }

        private void textBox等待转码视频文件夹_DragEnter(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effect = DragDropEffects.All;
            } else
                e.Effect = DragDropEffects.None;
        }

        private void textBox日志_KeyUp(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.F5) {
                if (thread切片.IsAlive && thread转码.IsAlive && thread合并.IsAlive) {
                    if (转码队列.Has汇总输出信息(out string str编码速度)) {
                        textBox日志.Text = str编码速度;
                    }
                    autoReset初始信息.Set( );
                }
            }
        }

        private void checkBox多线程_CheckedChanged(object sender, EventArgs e) {

        }
        private void checkBox多线程_MouseClick(object sender, MouseEventArgs e) {
            if (checkBox多线程.Checked) {
                numericUpDown_Workers.Value = (NumberOfLogicalProcessors / libEnc选中.i默认线程数 + 1);
            } else {
                numericUpDown_Workers.Value = NumberOfLogicalProcessors;
                if (video热乎的切片 == null && video正在转码文件 == null)
                    add日志(libEnc选中.get参数_编码器预设画质(key选择预设: comboBox预设.Text, b多线程: checkBox多线程.Checked, b微调CRF: checkBox_DriftCRF.Checked, crf: numericUpDown_CRF.Value));
            }
        }

        public static LibEnc libEnc选中;
        private void comboBox_lib_SelectedIndexChanged(object sender, EventArgs e) {
            libEnc选中 = LibEnc.dic_编码库_初始设置[comboBox_lib.Text];

            comboBox预设.DataSource = libEnc选中.dic_选择_预设.Keys.ToArray( );
            comboBox预设.Text = libEnc选中.key显示预设;

            numericUpDown_CRF.DecimalPlaces = libEnc选中.CRF参数.i小数位;
            numericUpDown_CRF.Maximum = (decimal)libEnc选中.CRF参数.my_max;
            numericUpDown_CRF.Minimum = (decimal)libEnc选中.CRF参数.my_min;
            crf上次 = numericUpDown_CRF.Value = (decimal)libEnc选中.CRF参数.my_value;

            textBox日志.Text = libEnc选中.str画质参考;

            if (libEnc选中.i默认线程数 == 1) {
                checkBox多线程.Checked = false;
                checkBox多线程.Visible = false;
                numericUpDown_Workers.Value = NumberOfLogicalProcessors;
            } else {
                checkBox多线程.Visible = true;
                if (libEnc选中.b多线程优先) {
                    checkBox多线程.Checked = true;
                    numericUpDown_Workers.Value = NumberOfLogicalProcessors / libEnc选中.i默认线程数 + 1;
                } else {
                    checkBox多线程.Checked = false;
                    numericUpDown_Workers.Value = NumberOfLogicalProcessors;
                }
            }

            if (libEnc选中.Noise去除参数 == null) {
                checkBox_磨皮.Visible = trackBar_降噪量.Visible = false;
                if (video热乎的切片 == null && video正在转码文件 == null)
                    add日志(libEnc选中.get参数_编码器预设画质(libEnc选中.key显示预设, libEnc选中.b多线程优先, checkBox_DriftCRF.Checked, crf上次));
            } else {
                checkBox_磨皮.Visible = trackBar_降噪量.Visible = true;
                checkBox_磨皮.Checked = libEnc选中.Noise去除参数.b默启;
                trackBar_降噪量.Minimum = libEnc选中.Noise去除参数.min;
                trackBar_降噪量.Maximum = libEnc选中.Noise去除参数.max;
                trackBar_降噪量.Value = libEnc选中.Noise去除参数.def;

                if (video热乎的切片 == null && video正在转码文件 == null)
                    add日志(libEnc选中.get参数_编码器预设画质(libEnc选中.key显示预设, libEnc选中.b多线程优先, checkBox_DriftCRF.Checked, crf上次, libEnc选中.Noise去除参数.b默启, libEnc选中.Noise去除参数.def));
            }


        }

        private void checkBox_lavfi_CheckedChanged(object sender, EventArgs e) {
            textBox_lavfi.Visible = checkBox_lavfi.Checked;
        }
        private void Form破片压缩_Resize(object sender, EventArgs e) {
            if (WindowState == FormWindowState.Minimized) {
                timer刷新编码输出.Stop( );
                b最小化 = true;
            } else if (b最小化) {//从最小化恢复时触发一次时钟启动
                b最小化 = false;
                if (转码队列.Has汇总输出信息(out string str编码速度)) {
                    textBox日志.Text = str编码速度;
                }
                timer刷新编码输出.Start( );
            }
        }
        private void textBox日志_Enter(object sender, EventArgs e) {
            timer刷新编码输出.Interval = 33333;
        }
        private void textBox日志_Leave(object sender, EventArgs e) {
            timer刷新编码输出.Interval = 8888;
        }
        private void Form破片压缩_Activated(object sender, EventArgs e) {
            timer刷新编码输出.Interval = 6666;
            if (timer刷新编码输出.Enabled) {
                if (转码队列.Has汇总输出信息(out string str编码速度)) {
                    textBox日志.Text = str编码速度;
                }
            }
        }
        private void Form破片压缩_Deactivate(object sender, EventArgs e) {
            timer刷新编码输出.Interval = 66666;
            if (timer刷新编码输出.Enabled && str最后一条信息 != "刷新输出信息间隔调整为一分钟")
                add日志("刷新输出信息间隔调整为一分钟");
        }
        private void Form破片压缩_Load(object sender, EventArgs e) {
            CPUNum( );
            //Extract_EXE.resources_to_exe( );
            comboBox_lib.SelectedIndex = 0;
            comboBox_Crop.SelectedIndex = 0;
            //comboBox切片模式.SelectedIndex = 3;//30秒
            comboBox切片模式.SelectedIndex = 8;//无缓转码

            Text += Application.ProductVersion;

            FileInfo fi破片转码文件夹 = new FileInfo("破片转码文件夹.txt");
            if (fi破片转码文件夹.Exists && fi破片转码文件夹.Length < 1048576) {
                string txt = null;
                try { txt = File.ReadAllText("破片转码文件夹.txt").Trim( ); } catch { }
                if (!string.IsNullOrEmpty(txt) && !string.Equals(txt, "E:\\Videos"))
                    textBox等待转码视频文件夹.Text = txt;
            }

            thread初始信息 = new Thread(new ThreadStart(fn初始信息));
            thread初始信息.IsBackground = true;
            thread初始信息.Start( );
        }



    }
}
