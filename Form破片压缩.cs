using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;


namespace 破片压缩器 {
    public partial class Form破片压缩: Form {

        public static bool b保存异常日志 = true;

        int i切片间隔秒 = 60;
        decimal d检测镜头精度 = new decimal(0.1);

        bool b更改过文件夹 = true, b需要重扫 = false, b最小化 = false;

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
            autoReset合并 = new AutoResetEvent(false);

        List<DirectoryInfo> list输入文件夹 = new List<DirectoryInfo>( );

        Thread thread切片, thread转码, thread合并;

        object obj转码队列 = new object( ), obj合并队列 = new object( );

        Video_Roadmap video正在转码文件 = null;
        List<Video_Roadmap> list_等待转码队列 = new List<Video_Roadmap>( );
        List<Video_Roadmap> list_等待合并队列 = new List<Video_Roadmap>( );

        public Form破片压缩( ) {
            InitializeComponent( );

            thread切片 = new Thread(fn后台切片);
            thread切片.IsBackground = true;
            thread切片.Name = "切片";

            thread转码 = new Thread(fn后台转码);
            thread转码.IsBackground = true;
            thread转码.Name = "转码";

            thread合并 = new Thread(fn后台合并);
            thread合并.IsBackground = true;
            thread合并.Name = "合并";

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
        bool is队列中(FileInfo file) {
            string name = file.Name.ToLower( );
            if (name.Contains("svtav1") || name.Contains("aomav1")) return true;

            string lower完整路径_输入视频 = file.FullName.ToLower( );

            if (video正在转码文件 != null && lower完整路径_输入视频 == video正在转码文件.lower完整路径_输入视频) return true;

            for (int i = 0; i < list_等待转码队列.Count; i++)
                if (lower完整路径_输入视频 == list_等待转码队列[i].lower完整路径_输入视频) return true;

            for (int i = 0; i < list_等待合并队列.Count; i++)
                if (lower完整路径_输入视频 == list_等待合并队列[i].lower完整路径_输入视频) return true;

            return false;
        }

        bool is缓存低 {
            get {
                int i剩余切片数量 = 转码队列.i并发任务数;
                if (video正在转码文件 != null) i剩余切片数量 += video正在转码文件.i剩余切片数量 + 1;
                for (int i = 0; i < list_等待转码队列.Count; i++) i剩余切片数量 += list_等待转码队列[i].i剩余切片数量;
                转码队列.i切片缓存 = i剩余切片数量;
                return i剩余切片数量 < f保底缓存切片;
            }
        }

        void fn后台切片( ) {
            while (true) {
                更改过文件夹: b更改过文件夹 = b需要重扫 = false;
                lock (obj转码队列) { list_等待转码队列.Clear( ); }
                DirectoryInfo[] arrDir = list输入文件夹.ToArray( );
                foreach (DirectoryInfo dir in arrDir) {
                    if (!Directory.Exists(dir.FullName)) continue;//此处循环存在等待时长，文件夹有被移动风险。判断一次文件夹存在情况。
                    add日志($"查找视频:{dir.FullName}");
                    FileInfo[] arrFileInfo = dir.GetFiles( );
                    foreach (FileInfo file in arrFileInfo) {
                        if (Video_Roadmap.is视频(file) && !is队列中(file)) {//每切片视频后，存在长时间等待编码结束。 Video_Roadmap.is视频内有判断一次视频存在情况。
                            str正在转码文件夹 = $"{str切片根目录}\\{file.DirectoryName.Replace(file.Directory.Root.FullName, "")}";
                            Video_Roadmap video = new Video_Roadmap(file, str正在转码文件夹);
                            if (!video.b解码60帧判断交错(out StringBuilder builder)) //扫描60帧，出结果较快。
                                video.b读取视频头(out builder);

                            if (!video.b有切片记录) {//如果找不到现有切片，先进行切片。
                                if (Settings.b扫描场景) {
                                    string log = "扫描关键帧差异，决定切片场景：" + file.Name;
                                    add日志(log);
                                    if (!转码队列.b有任务) new Thread(fn初始信息).Start( );//扫描场景有点费时间，增加进度输出。
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
                                    if (!转码队列.b有任务) new Thread(fn初始信息).Start( );//切片大文件有点费时间，增加进度输出。
                                    video.Fx按时间切片并获取列表(i切片间隔秒, ref builder);//当视频体积非常大时，切片耗时较长，软件完全看不出进度
                                }
                            }

                            if (video.b有切片记录) {
                                if (video.i剩余切片数量 > 0) add日志($"恭喜！获得视频碎片{video.i剩余切片数量}片 @{file.FullName}");
                                if (!video.b查找MKA音轨( )) {
                                    add日志($"提取音轨：{video.strMKA文件名}");
                                    video.b提取MKA音轨(ref builder);
                                    //使用提取音轨消耗时间，让之前切片缓存完全写入磁盘。
                                }//mkvmerge小概率返回结果后，内存中的数据未完全写入磁盘。已增加命令行 --flush-on-close 完整写入磁盘退出

                                txt日志(builder.ToString( ));
                                lock (obj转码队列) { list_等待转码队列.Add(video); }
                                autoReset转码.Set( );
                                fx循环等待切片不足( );
                            } else {
                                add日志("切片异常：" + file.Name);
                            }
                        }
                        if (b更改过文件夹) goto 更改过文件夹;//中途更改文件夹优先级高，立刻跳出重新扫描
                    }
                }
                add日志($"输入目录已全部扫描！");
                while (!b需要重扫) autoReset切片.WaitOne( );
            }
        }//1号线程，准备好了切片，后续线程才能顺序调度。

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

                    if (Settings.b自动裁黑边) {
                        add日志($"扫描黑边：{video正在转码文件.fi输入视频.Name}");
                        video正在转码文件.b扫描视频黑边生成剪裁参数( );
                    }
                    video正在转码文件.b拼接转码摘要( );
                    timer刷新编码输出.Start( );

                    if (video正在转码文件.b后台转码MKA音轨( )) {//单独转码OPUS音轨，CPU资源占用少，放在视频队列之前。
                        add日志($"转码音轨：{video正在转码文件.strMKA路径}");
                    }

                    while (video正在转码文件.b转码下一个切片(out External_Process external_Process)) {
                        转码队列.ffmpeg等待入队(external_Process);//有队列上限
                        if (is缓存低) autoReset切片.Set( );
                        add日志($"开始转码：{external_Process.fi编码.FullName}");
                    }
                    lock (obj合并队列) { list_等待合并队列.Add(video正在转码文件); }
                }
                add日志("切片皆加入转码任务，等待合并中。加入新视频需点刷新按钮！");
                autoReset合并.Set( );
            }

        }//2号线程，一个目录下转码完成，调度3号线程。

        void fn协同视频转码( ) {//0号线程想设计为局域网多分机读取切片，转未处理的不同碎片，转完汇入主机合并。
            //两套构想方案
            //1。通过HTTP通信，主分机通过数据交换，转码任务主→从顺序分配。
            //2。通过尝试移动碎片各自工作文件夹，分机自主处理各自任务，存在任务碎片化加剧问题。等待合并过程拉长。
        }

        void fn后台合并( ) {
            StringBuilder sb合并 = new StringBuilder( );
            while (true) {
                autoReset合并.WaitOne( );//合并等待

                for (int i = 0; i < list_等待合并队列.Count;) {
                    if (list_等待合并队列[i].b音轨需要更新) {
                        add日志($"转码音轨：{list_等待合并队列[i].str切片路径}");
                        list_等待合并队列[i].b更新OPUS音轨( );//音轨设置可以在视频转码过程中更改，即刻生效。
                    }

                    if (list_等待合并队列[i].b文件夹下还有切片) {
                        i++;
                    } else {
                        add日志($"开始合并：{list_等待合并队列[i].str切片路径}");
                        string str合并结果;
                        if (list_等待合并队列[i].b转码后混流( )) {//混流任务属于磁盘读写任务，理论上会和解流任务抢占资源。
                            str合并结果 = $"合并成功：{list_等待合并队列[i].str切片路径}\\{list_等待合并队列[i].str输出文件名}";
                        } else {
                            str合并结果 = $"合并失败！{list_等待合并队列[i].str切片路径}";
                        }

                        add日志(str合并结果);
                        sb合并.AppendLine(str合并结果);
                        lock (obj合并队列) { list_等待合并队列.RemoveAt(i); }
                    }
                }


                if (!b需要重扫 && list_等待合并队列.Count == 0 && !转码队列.b有任务) {

                    timer刷新编码输出.Stop( );
                    转码队列.Has汇总输出信息(out string str编码信息);
                    txt日志($"{str编码信息}\r\n\r\n目录下视频已完成，增加新视频点击刷新按钮\r\n\r\n{sb合并.ToString( )}");
                }
            }
        }//3号线程，任务收尾工作。

        void fn初始信息( ) {
            Thread.Sleep(9999);
            while (转码队列.Get独立进程输出(out string info)) {
                textBox日志.Invoke(new Action(( ) => {
                    textBox日志.Text = info;
                    textBox日志.SelectionStart = textBox日志.TextLength - 1;
                    textBox日志.ScrollToCaret( );
                }));
                Thread.Sleep(9999);

            }
        }

        void fx循环等待切片不足( ) {
            刷新缓存:
            int i剩余缓存 = 转码队列.i并发任务数;
            if (video正在转码文件 != null) i剩余缓存 += video正在转码文件.i剩余切片数量 + 1;
            for (int i = 0; i < list_等待转码队列.Count; i++) i剩余缓存 += list_等待转码队列[i].i剩余切片数量;
            if (i剩余缓存 > f保底缓存切片) {
                add日志($"切片数量充足：{转码队列.i并发任务数} / {i剩余缓存}");
                autoReset切片.WaitOne( );
                goto 刷新缓存;
            }
            转码队列.i切片缓存 = i剩余缓存;
            add日志($"切片缓存：{转码队列.i并发任务数} / {i剩余缓存}，查找下一视频……");
        }

        void CPUNum( ) {
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_ComputerSystem").Get( )) {
                NumberOfProcessors += int.Parse(item["NumberOfProcessors"].ToString( ));
            }
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get( )) {
                NumberOfCores += int.Parse(item["NumberOfCores"].ToString( ));
            }
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_ComputerSystem").Get( )) {
                NumberOfLogicalProcessors += int.Parse(item["NumberOfLogicalProcessors"].ToString( ));
            }
            转码队列.i物理核心数 = NumberOfCores;
            add日志($"{NumberOfProcessors}处理器 {NumberOfCores}核心 {NumberOfLogicalProcessors}线程");


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

            if (NumberOfLogicalProcessors > 2) {//非常见例子，核心数=9 显示1、9、8 默认8核心
                comboBox_Workers.Items.Add(NumberOfLogicalProcessors);
                if (NumberOfLogicalProcessors > NumberOfCores)
                    comboBox_Workers.Items.Add(NumberOfCores);

                comboBox_Workers.Items.Add(NumberOfLogicalProcessors - 1);
                comboBox_Workers.Items.Add(1);

                int i间隔 = NumberOfLogicalProcessors < 10 ? 1 : NumberOfLogicalProcessors / 8;//8核心以下，全部显示;
                for (int i = NumberOfLogicalProcessors - 2; i > 1; i -= i间隔) {
                    comboBox_Workers.Items.Add(i);
                }
                comboBox_Workers.Items.Add(0);//0进程为走完最后一个停止
            } else {
                comboBox_Workers.Items.Add(1);
            }
            comboBox_Workers.SelectedIndex = 0;
        }


        bool fx文件夹( ) {
            string txt = textBox等待转码视频文件夹.Text.Trim( );
            if (txt等待转码视频文件夹 != txt) {
                txt等待转码视频文件夹 = txt;
                string[] arrPath = txt.Split('\n');
                HashSet<string> set小写路径 = new HashSet<string>( );
                List<DirectoryInfo> list_Temp_di = new List<DirectoryInfo>( );
                for (int i = 0; i < arrPath.Length; i++) {
                    string path = arrPath[i].Trim( );
                    if (path.Length > 3) {// E:\1
                        DirectoryInfo di;
                        try { di = new DirectoryInfo(arrPath[i]); } catch { continue; }
                        if (di.Exists && di.Root.FullName != di.FullName) {
                            if (set小写路径.Add(di.FullName.ToLower( ))) {
                                list_Temp_di.Add(di);
                            }
                        }
                    }
                }

                if (list_Temp_di.Count != list输入文件夹.Count) {
                    b更改过文件夹 = true;//扫描优先级高，扫完一个文件就判断一次，重扫指定顺序
                } else {
                    for (int i = 0; i < list_Temp_di.Count; i++) {
                        if (list_Temp_di[i].FullName != list输入文件夹[i].FullName) {//此处包含扫描路径读取顺序更改
                            b更改过文件夹 = true;
                            break;
                        }
                    }
                }
                list输入文件夹 = list_Temp_di;
            }
            return b需要重扫 = list输入文件夹.Count > 0;//重扫优先级低，当点过刷新按钮，判断一下有输入文件夹就重新扫描一遍。
        }

        void fx刷新设置( ) {
            d检测镜头精度 = numericUpDown检测镜头.Value;

            if (int.TryParse(comboBox_Workers.Text, out int i多进程数量)) {
                转码队列.i多进程数量 = i多进程数量;
                f保底缓存切片 = i多进程数量 * 1.2f + 1;
            }

            Settings.b单线程 = !comboBox_lib.Text.Contains("多线程");

            Settings.b磨皮降噪 = checkBox_磨皮.Checked;
            Settings.i降噪强度 = trackBar_降噪量.Value;

            Settings.b转可变帧率 = true;
            Settings.b自定义滤镜 = checkBox_lavfi.Checked;
            Settings.b根据帧率自动强化CRF = checkBox_DriftCRF.Checked;
            Settings.str自定义滤镜 = textBox_lavfi.Text.Trim( ).Trim(',');
            Settings.opus = checkBoxOpus.Checked;
            Settings.b音轨同时切片转码 = checkBoxSplitAudio.Checked;

            Settings.i音频码率 = (int)numericUpDown_AB.Value;
            Settings.crf = (int)numericUpDown_CRF.Value;

            Settings.i音频码率 = (int)numericUpDown_AB.Value;
            Settings.i剪裁后宽 = (int)numericUpDown_Width.Value;
            Settings.i剪裁后高 = (int)numericUpDown_Height.Value;
            Settings.i左裁像素 = (int)numericUpDown_Left.Value;
            Settings.i上裁像素 = (int)numericUpDown_Top.Value;
            Settings.i缩小到宽 = (int)numericUpDown_ScaleW.Value;
            Settings.i缩小到高 = (int)numericUpDown_ScaleH.Value;


            Settings.b右上角文件名_切片序列号水印 = checkBox_drawtext.Checked;

            Settings.i长边 = Settings.i缩小到宽;
            Settings.b长边像素 = label_ScaleW.Text == "长边像素";

            comboBox_Scale.Text = Settings.str缩小文本;

            Settings.speed = comboBoxSpeed.SelectedIndex;
            if (Settings.speed < 0 || Settings.speed > 8) {
                Settings.speed = 4; Settings.speedTxt = "中";
            } else
                Settings.speedTxt = comboBoxSpeed.Text;

            Settings.crf = (int)numericUpDown_CRF.Value;
            Settings.b转可变帧率 = checkBox_VFR.Checked;

            Settings.sec_gop = (int)numericUpDown_GOP.Value;
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
                if (thread切片.IsAlive && thread转码.IsAlive && thread合并.IsAlive) {
                    转码队列.autoReset入队.Set( );
                    autoReset切片.Set( );
                    autoReset合并.Set( );
                    if (转码队列.Has汇总输出信息(out string str编码速度)) {
                        textBox日志.Text = str编码速度;
                    }
                    timer刷新编码输出.Start( );
                } else {
                    if (Video_Roadmap.b查找可执行文件(out string log)) {
                        button刷新.Text = "刷新(&R)";
                        thread切片.Start( );
                        thread转码.Start( );
                        thread合并.Start( );
                        timer刷新编码输出.Enabled = true;
                    } else {
                        add日志($"需要在工具同目录放入：" + log);
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
            d检测镜头精度 = numericUpDown检测镜头.Value;

            int SelectedIndex = comboBox切片模式.SelectedIndex;

            if (SelectedIndex == 0) {
                i切片间隔秒 = Settings.sec_gop * 10;
                Settings.b扫描场景 = true;
                numericUpDown检测镜头.Visible = true;
            } else {
                Settings.b扫描场景 = false;
                numericUpDown检测镜头.Visible = false;
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

            panel_Top.Visible = show;
            panel_Left.Visible = show;

            panel_Height.Visible = show;
            panel_Width.Visible = show;//不可更改显示顺序，界面对齐

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

        private void trackBar_降噪量_Scroll(object sender, EventArgs e) {
            int n = trackBar_降噪量.Value;
            if (checkBox_磨皮.Checked && n > 0) {
                checkBox_磨皮.Text = "磨皮降噪×" + n + "（会大幅降低速度）";
            } else {
                checkBox_磨皮.Text = "磨皮降噪，会大幅降低速度";
            }
        }

        private void checkBox_磨皮_CheckedChanged(object sender, EventArgs e) {
            if (checkBox_磨皮.Checked) {
                trackBar_降噪量.Value = 4;
                trackBar_降噪量.Visible = true;
                checkBox_磨皮.Text = "磨皮降噪×4（会大幅降低速度）";
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

        private void textBox日志_KeyUp(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.F5) {
                if (thread切片.IsAlive && thread转码.IsAlive && thread合并.IsAlive) {
                    if (转码队列.Has汇总输出信息(out string str编码速度)) {
                        textBox日志.Text = str编码速度;
                    }
                }
            }
        }

        private void comboBox_lib_SelectedIndexChanged(object sender, EventArgs e) {
            string lib = comboBox_lib.Text;
            string tips = string.Empty;
            comboBox_Workers.SelectedIndex = 0;

            if (lib.Contains("libaom")) {
                //checkBox_磨皮.Visible = true;
                comboBoxSpeed.SelectedIndex = 2; //cpu-used 3和4速度差不多，3比4小1%+，2比3小5%。
                numericUpDown_CRF.Maximum = 63;
                numericUpDown_CRF.Minimum = 0;
                numericUpDown_CRF.Value = 32;
                Settings.str视频编码库 = "aomav1";
                //config.partition_n = 2;
                //int i视觉无损 = 23, i轻损 = 28, i忍损 = 35;//aomenc,crf固定，cpu-used不同，质量区别无法肉眼察觉，速度、体积可观测。
                tips = "aomenc画质范围参考↓\r\n蓝光原盘：CRF=8\r\n视觉无损：CRF=16\r\n超清：\tCRF=23\r\n高清：\tCRF=28（推荐）\r\n标清：\tCRF=32（默认）";

                if (lib.Contains("多线程")) {
                    comboBox_Workers.Text = (NumberOfLogicalProcessors / 3 + 1).ToString( );
                }
            } else if (lib.Contains("libsvt")) {
                //comboBoxSpeed.SelectedIndex = 5;//prest 5 速度快画质能接受
                comboBoxSpeed.SelectedIndex = 3;//prest 3 速度比aom p3快，质量接近
                numericUpDown_CRF.Maximum = 63;
                numericUpDown_CRF.Minimum = 1;
                numericUpDown_CRF.Value = 35;
                Settings.str视频编码库 = "svtav1";
                //config.partition_n = 4;
                //int i视觉无损 = 23, i轻损 = 28, i忍损 = 35;
                tips = "SVT-AV1画质范围参考↓\r\n蓝光原盘：CRF=8\r\n视觉无损：CRF=18\r\n超清：\tCRF=25\r\n高清：\tCRF=30（推荐）\r\n标清：\tCRF=35（默认）";

                if (lib.Contains("多线程")) {
                    comboBox_Workers.Text = (NumberOfLogicalProcessors / 16 + 1).ToString( );
                }
            }


            textBox日志.Text = tips;
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
            //if (timer刷新编码输出.Enabled) add日志("刷新输出信息间隔调整为30秒");
        }
        private void textBox日志_Leave(object sender, EventArgs e) {
            timer刷新编码输出.Interval = 8888;
            //if (timer刷新编码输出.Enabled) add日志("刷新输出信息间隔调整为8秒");
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
            comboBox切片模式.SelectedIndex = 4;
            comboBox_Crop.SelectedIndex = 0;
            comboBox_lib.SelectedIndex = 0;
            this.Text += Application.ProductVersion;
        }
    }
}
