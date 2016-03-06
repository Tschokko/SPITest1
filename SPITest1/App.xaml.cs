using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Spi;
using Windows.Devices.Gpio;
using Windows.Devices.Enumeration;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SPITest1
{
    sealed partial class App : Application
    {
        private const string SPI_CONTROLLER_NAME = "SPI0";
        private const Int32 SPI_CHIP_SELECT_LINE = 0;

        private BMP280DigitalPressureSensor sensor;
        private GpioController IoController;

        public App()
        {
            this.InitializeComponent();

            InitGpio();
            InitSPI();
        }

        private async void InitSPI()
        {
            try
            {
                // Init BMP280 SPI-based sensor
                sensor = new BMP280DigitalPressureSensor(SPI_CONTROLLER_NAME, SPI_CHIP_SELECT_LINE);
                await sensor.Initialize();
                double temp = await sensor.GetTemperatureOnce(BMP280Oversampling.X1);
                Debug.WriteLine("Current temp: " + temp);
            }
            catch (Exception ex)
            {
                throw new Exception("BMP280 sensor initialization Failed", ex);
            }
        }

        private void InitGpio()
        {
            IoController = GpioController.GetDefault(); // Get the default GPIO controller on the system
            if (IoController == null)
            {
                throw new Exception("GPIO does not exist on the current system.");
            }
        }
    }
}
