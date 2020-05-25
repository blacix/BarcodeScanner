using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Scanner
{
    public class SerialPortHandler
    {
        private static readonly int BUFFER_SIZE = 256;
        public string PortName { get; private set; }
        private SerialPort Port = new SerialPort();
        private List<byte> SerialBuffer;

        private MainWindow Window;
        private readonly SynchronizationContext Context;

        // constructs a handler with the desired name
        // check device manager
        // eg: COM5
        public SerialPortHandler(string name, MainWindow window)
        {
            Window = window;
            Context = SynchronizationContext.Current;
            PortName = name;
            SerialBuffer = new List<byte>(BUFFER_SIZE);
            Port = new SerialPort(PortName); // check device com port
            
            // set baud rate
            Port.BaudRate = 9600;

            // config other stuff
            // probably not needed
            // Port.NewLine = "\r\n";
            //Port.DtrEnable = false;
            //Port.RtsEnable = true;
            //Port.Parity = Parity.Odd;
            //Port.Encoding = System.Text.Encoding.ASCII;
            //Port.DiscardNull = false;
        }

        // call it to open the serial port
        public bool startListen()
        {
            if (!Port.IsOpen)
            {
                try
                {
                    SerialBuffer = new List<byte>(BUFFER_SIZE);
                    Port.Open();
                    // register callback for reveiving data
                    Port.DataReceived += this.handleSerialPortDataReceived;
                }
                catch (System.IO.IOException)
                {
                   Console.WriteLine("IOException: " + "Could not OPEN serial port!" + PortName);
                }
                catch (Exception)
                {
                    Console.WriteLine("Unknown Exception: " + "Could not OPEN serial port!" + PortName);
                }
            }
            return Port.IsOpen;
        }

        // callback to receive data from the serial port
        // this is called by the platform from another thread!!!!!
        // Context variable can pass back the data to the GUI thread
        private void handleSerialPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int c = 0;

            // probably we should read until end of line character: '\n' or maybe Environment.NewLine
            //while (c != '\n')

            // this will just read everything
            while (Port.IsOpen && Port.BytesToRead > 0)
            {
                c = Port.ReadByte();
                SerialBuffer.Add((byte)(0xFF & c));
            }
            // here SerialBuffer contains all the data received

            // pass data to UI thread
            Context.Post(new SendOrPostCallback((o) =>
            {
                Window.ScanResult(SerialBuffer);
            }), null);
        }

        // call to close serial port
        void stopListen()
        {
            Port.DataReceived -= this.handleSerialPortDataReceived;
            try
            {
                Port.DiscardOutBuffer();
                if (Port.IsOpen)
                    Port.Close();

                Console.WriteLine("port closed");
            }
            catch (Exception e)
            {
                Console.WriteLine("close Exception: " + e.Message);
            }
        }
    }
}
