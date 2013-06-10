using System;
using System.Collections.Generic;
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

using System.Diagnostics;
using System.Collections;
using System.Windows.Threading;
using System.IO.Ports;
using ELFSharp;
using ELFSharp.Sections;
using System.Text.RegularExpressions;

namespace LogCatcher
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        SerialPort com = null;
        private int[] baudRates = { 9600, 115200 };

        // Elf file processing data
        //Dictionary<string, string> elfFuncDict = new Dictionary<string, string>();
        Dictionary<string, UInt32> elfFuncDict = new Dictionary<string,uint>();
        //Dictionary<string, string>.ValueCollection elfFuncValues;
        List<UInt32> elfFuncValuesList = new List<uint>();
        Regex regexAddr = new Regex(@"(0x[\dabcdefABCDEF]{8})");

        string elfFileName;
        string outputFileName;

        public MainWindow()
        {
            InitializeComponent();
            this.Reset();
        }

        private void FillElfFuncDict()
        {
            var elf = ELFReader.Load("ucos_ii_s3c2440.axf");
            var functions = ((ISymbolTable)elf.GetSection(".symtab")).Entries.Where(x => x.Type == SymbolType.Function);
            string[] funcStrs;
            uint addr;
            //Dictionary<string, UInt32> tempDict = new Dictionary<string, UInt32>();

            foreach (var f in functions)
            {
                funcStrs = f.ToString().Split(new char[3] { ' ', ',', ':' });
                addr = Convert.ToUInt32(funcStrs[4], 16);
                Debug.WriteLine(String.Format("{0} {1}\r\n", funcStrs[2], addr));
                elfFuncDict.Add(funcStrs[2], addr);
                elfFuncValuesList.Add(addr);
            }
            /*
            elfFuncKeyValuePair = from pair in tempDict
                                  orderby pair.Value
                                  select pair;
             */
            elfFuncValuesList.Sort();
            Debug.WriteLine("[LogCatcher] Elf Process Done.\r\n");
        }

        private void Reset()
        {
            foreach (string portName in SerialPort.GetPortNames())
            {
                portNameComboBox.Items.Add(portName);
            }
            foreach (Int32 baudRate in baudRates)
            {
                baudRateComboBox.Items.Add(baudRate.ToString());
            }
            portNameComboBox.SelectedIndex = 0;
            baudRateComboBox.SelectedIndex = 0;

            com = new SerialPort();
            com.PortName = portNameComboBox.Items[0] as string;
            com.Parity = Parity.None;
            com.StopBits = StopBits.One;
            com.DataBits = 8;
            com.Handshake = Handshake.None;
        }

        private void startCatchButton_Click(object sender, RoutedEventArgs e)
        {
            com.PortName = portNameComboBox.Text;
            com.BaudRate = Convert.ToInt32(baudRateComboBox.Text);
            com.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
            // Test ElfCSharp
            //foreach (var f in functions)
            {
                
                //dataTextBox.AppendText(String.Format("{0} {1}\r\n", f.Name, r.Match(f.ToString())));
                //Console.WriteLine(String.Format("{0} {1}\r\n", f.Name, regExAddr.Match(f.ToString())));
            }
            FillElfFuncDict();

            com.Open();
            com.Write("a");
            startCatchButton.IsEnabled = false;
        }
        
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = sender as SerialPort;

            string strLine = sp.ReadLine();
            Debug.WriteLine(String.Format("[SerialPort Receive]<{0}>\r\n", strLine));

            strLine = AddFunctionNameAndAddrOffset(strLine);

            this.Dispatcher.Invoke(new Action(() =>
            {
                dataTextBox.AppendText(strLine);
            }), DispatcherPriority.ApplicationIdle);
        }

        private string AddFunctionNameAndAddrOffset(string str)
        {
            UInt32 addr;
            int addrOffsetIndex;
            uint addrOffset;

            Match m = regexAddr.Match(str);
            if (m != Match.Empty)
            {
                addr = Convert.ToUInt32(m.ToString(), 16);
                addrOffsetIndex = elfFuncValuesList.BinarySearch(addr);
                if (addrOffsetIndex < 0)
                {
                    addrOffsetIndex = ~addrOffsetIndex - 1; //the first large number index - 1, 对返回的负数所执行的按位求补操作（C# 和 Visual C++ 中为 ~ 运算符，在 Visual Basic 中为 Xor -1）将产生列表中大于搜索字符串的第一个元素的索引
                }
                addrOffset = addr - elfFuncValuesList[addrOffsetIndex];

                var funcNames = from pair in elfFuncDict
                                where pair.Value == elfFuncValuesList[addrOffsetIndex]
                                select pair.Key;

                foreach (string f in funcNames)
                {
                    Debug.WriteLine(String.Format("[ELF function name] {0}\r\n", f));
                    str = regexAddr.Replace(str, String.Format("{0} {1}()(0x{2:x})", m.Groups[0], f, addrOffset));
                }
            }
            return str;
        }
    }
}
