using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

namespace Scanner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string DirectoryPath;
        UsbDevices UsbHandler;

        public MainWindow()
        {
            InitializeComponent();

            UsbHandler = new UsbDevices();
            string seiralPortName = UsbHandler.GetScannerSerialPortName();

            // atach/remove does not matter for now...
            //UsbHandler.UsbDevicesChangedEvent += this.HandleUsbDevicesChanged;
            SerialPortHandler sh = new SerialPortHandler(seiralPortName, this);
            sh.startListen();            
        }

        private void textBoxUserName_TextChanged(object sender, TextChangedEventArgs e)
        {
            assembleEntry();
        }

        private void textBoxSolarPanelID_TextChanged(object sender, TextChangedEventArgs e)
        {
            assembleEntry();
        }


        private void textBoxPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            assembleFileName();
        }

        private void textBoxUserName_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            assembleFileName();
        }

        private void buttonSubmit_Click(object sender, RoutedEventArgs e)
        {
            string tmp = textBoxCurrentFileName.Text;
            using (StreamWriter sw = File.AppendText(tmp))
            {
                sw.WriteLine(textBoxNewEntry.Text);
            }

            textBoxSolarPanelID.Clear();
            textBoxNewEntry.Clear();
        }

        public void ScanResult(List<byte> buffer)
        {

        }

        private void assembleFileName()
        {
            DateTime now = DateTime.Now;
            DirectoryPath = textBoxPath.Text;
            if (DirectoryPath.Length > 0 && DirectoryPath[DirectoryPath.Length - 1] != '\\')
                DirectoryPath += "\\";

            textBoxCurrentFileName.Text = DirectoryPath
                + now.ToShortDateString() + "-"
                + textBoxUserName.Text + ".txt";
        }

        private void assembleEntry()
        {
            DateTime now = DateTime.Now;
            textBoxNewEntry.Text = now.ToString() + "-" +  
                textBoxUserName.Text + "-" +
                textBoxSolarPanelID.Text;
        }



        public void HandleUsbDevicesChanged(object sender, EventArgs e)
        {

        }
    }
}
