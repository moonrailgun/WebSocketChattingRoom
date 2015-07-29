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
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnOpenWSServer(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug("正在启动WebSocket服务器...");
            }
            catch (Exception ex)
            {
                Debug("发生异常:" + ex.ToString());
            }
        }

        private void OnCloseWSServer(object sender, RoutedEventArgs e)
        {
            Debug("正在关闭WebSocket服务器...");
        }

        private void Debug(string message)
        {
            DateTime time = DateTime.Now;

            int index = this.MessageWindow.Items.Add(string.Format("[{0}]{1}", time.ToString("HH:mm:ss"), message));
            this.MessageWindow.ScrollIntoView(this.MessageWindow.Items[index]);
        }
    }
}
