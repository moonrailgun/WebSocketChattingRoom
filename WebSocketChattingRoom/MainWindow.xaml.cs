using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;

namespace WebSocketChattingRoom
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow instance;//对外可以访问的实例
        private TcpHelper tcpHelper;

        public MainWindow()
        {
            InitializeComponent();
            instance = this;
            tcpHelper = new TcpHelper();
        }

        private void OnOpenWSServer(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug("正在启动WebSocket服务器...");
                tcpHelper.Run(8080);
                Debug("WebSocket服务器启动完毕");
            }
            catch (Exception ex)
            {
                Debug("发生异常:" + ex.ToString(), LogLevel.ERROR);
            }
        }

        private void OnCloseWSServer(object sender, RoutedEventArgs e)
        {
            Debug("正在关闭WebSocket服务器...");
            tcpHelper.Abort();
        }

        private void Debug(string message,LogLevel level = LogLevel.INFO)
        {
            LogsSystem.Instance.Print(message, level);
        }

        private void ClearList(object sender, RoutedEventArgs e)
        {
            this.MessageWindow.Items.Clear();
        }

        private void OpenSettingWindow(object sender, RoutedEventArgs e)
        {
            Setting settingWindow = new Setting();
            settingWindow.Show();
        }
    }
}
