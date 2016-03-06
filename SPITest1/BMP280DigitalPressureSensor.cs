using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;

namespace SPITest1
{
    public class BMP280CalibrationData
    {
        public UInt16 dig_T1 { get; set; }
        public Int16 dig_T2 { get; set; }
        public Int16 dig_T3 { get; set; }

        public UInt16 dig_P1 { get; set; }
        public Int16 dig_P2 { get; set; }
        public Int16 dig_P3 { get; set; }
        public Int16 dig_P4 { get; set; }
        public Int16 dig_P5 { get; set; }
        public Int16 dig_P6 { get; set; }
        public Int16 dig_P7 { get; set; }
        public Int16 dig_P8 { get; set; }
        public Int16 dig_P9 { get; set; }
    }

    public enum BMP280PowerMode : byte
    {
        Sleep = 0,
        Forced = 1,
        Normal = 3
    }

    public enum BMP280Oversampling : byte
    {
        Skipped = 0,
        X1 = 1,
        X2 = 2,
        x4 = 3,
        X8 = 4,
        X16 = 5
    }

    public class BMP280DigitalPressureSensor
    {
        private const byte BMP280_SIGNATURE = 0x58;

        // Calibration registers
        // These registers are read only and cannot be modified
        private const byte BMP280_REGISTER_DIG_T1 = 0x88;
        private const byte BMP280_REGISTER_DIG_T2 = 0x8A;
        private const byte BMP280_REGISTER_DIG_T3 = 0x8C;

        private const byte BMP280_REGISTER_DIG_P1 = 0x8E;
        private const byte BMP280_REGISTER_DIG_P2 = 0x90;
        private const byte BMP280_REGISTER_DIG_P3 = 0x92;
        private const byte BMP280_REGISTER_DIG_P4 = 0x94;
        private const byte BMP280_REGISTER_DIG_P5 = 0x96;
        private const byte BMP280_REGISTER_DIG_P6 = 0x98;
        private const byte BMP280_REGISTER_DIG_P7 = 0x9A;
        private const byte BMP280_REGISTER_DIG_P8 = 0x9C;
        private const byte BMP280_REGISTER_DIG_P9 = 0x9E;

        // Operationals registers
        private const byte BMP280_REGISTER_CHIPID = 0xD0;
        private const byte BMP280_REGISTER_CONTROL = 0xF4;

        private const byte BMP280_REGISTER_PRESSUREDATA_MSB = 0xF7;
        private const byte BMP280_REGISTER_PRESSUREDATA_LSB = 0xF8;
        private const byte BMP280_REGISTER_PRESSUREDATA_XLSB = 0xF9; // ony bits <7:4> are relevant

        private const byte BMP280_REGISTER_TEMPDATA_MSB = 0xFA;
        private const byte BMP280_REGISTER_TEMPDATA_LSB = 0xFB;
        private const byte BMP280_REGISTER_TEMPDATA_XLSB = 0xFC; // only bits <7:4> are relevant

        private const byte BMP280_REGISTER_HUMIDDATA_MSB = 0xFD;
        private const byte BMP280_REGISTER_HUMIDDATA_LSB = 0xFE;


        // Private members
        private string spiControllerName;
        private Int32 spiChipSelectLine;

        private SpiDevice spiDevice;
        private BMP280CalibrationData coefficeints;
        private bool isInitialized = false;
        private Int32 fineTemperature = Int32.MinValue; // Needed for computation against coefficeints

        public BMP280DigitalPressureSensor(string controllerName, Int32 chipSelectLine)
        {
            Debug.WriteLine("BMP280DigitalPressureSensor::BMP280DigitalPressureSensor");

            this.spiControllerName = controllerName;
            this.spiChipSelectLine = chipSelectLine;
        }

        public async Task Initialize()
        {
            Debug.WriteLine("BMP280DigitalPressureSensor::Initialize");

            var settings = new SpiConnectionSettings(spiChipSelectLine);
            settings.ClockFrequency = 10000000; // 10 MHz
            settings.Mode = SpiMode.Mode3;

            string spiAqs = SpiDevice.GetDeviceSelector(spiControllerName);
            var deviceInfo = await DeviceInformation.FindAllAsync(spiAqs);
            spiDevice = await SpiDevice.FromIdAsync(deviceInfo[0].Id, settings);
            
            // Check the device signature
            bool valid = await CheckSignature();
            if (!valid)
                throw new System.ArgumentException("Invalid device.");

            // Load calibration data
            coefficeints = await ReadCoefficeints();

            isInitialized = true;
        }

        private UInt16 ReadRegister(byte register)
        {
            Debug.WriteLine("BMP280DigitalPressureSensor::ReadRegister");

            UInt16 data = 0;
            byte[] writeBuffer = new byte[] { 0x00 };
            byte[] readBuffer = new byte[] { 0x00, 0x00 };

            // Setup control byte
            // ------------------
            // Place register into bit 0-6
            // Set bit 7 to 1 for read mode
            //   since or-pattern bit 7 is 1, value of bit 7 is always 1
            writeBuffer[0] = (byte)(register | (byte)(1 << 7));

            Debug.WriteLine("ReadRegister control byte: " +
                GetByteBinaryString(writeBuffer[0]));

            // Write control byte and receive 2 bytes of data
            spiDevice.TransferSequential(writeBuffer, readBuffer);

            Debug.WriteLine("ReadRegister data byte(s): {0}, {1}",
                GetByteBinaryString(readBuffer[0]),
                GetByteBinaryString(readBuffer[1]));

            int h = readBuffer[1] << 8;
            int l = readBuffer[0];
            data = (UInt16)(h + l);

            // Example how to extract the LSB and MSB from data
            // ------------------------------------------------
            // The BMP280 chip delivers two bytes of data on read. The first or lower byte equals
            // the requested register, the second order higher byte equals the register incremented by 1.
            // 
            // byte upper = (byte)(data >> 8);   // MSB, register 0xF5
            // byte lower = (byte)(data & 0xff); // LSB, register 0xF4
            return data;
        }

        private void WriteRegister(byte register, byte data)
        {
            Debug.WriteLine("BMP280DigitalPressureSensor::WriteRegister");

            byte[] writeBuffer = new byte[] { 0x00, 0x00 };
            byte[] readBuffer = new byte[] { 0x00 };

            // Setup control byte
            // ------------------
            // Place register into bits 0-6
            // Set bit 7 to 0 for write mode
            //   since and-pattern bits are continously 1 expect bit 7, value of bit 7 is always 0
            writeBuffer[0] = (byte)(register & (byte)(0xFF >> 1));

            Debug.WriteLine("WriteRegister control byte: " +
                GetByteBinaryString(writeBuffer[0]));

            // Set data to write
            writeBuffer[1] = data;

            Debug.WriteLine("WriteRegister data byte:    " +
                GetByteBinaryString(writeBuffer[1]));

            // Write control byte followed by data byte
            spiDevice.TransferSequential(writeBuffer, readBuffer);
        }

        private async Task<bool> CheckSignature()
        {
            Debug.WriteLine("BMP280DigitalPressureSensor::CheckSignature");

            UInt16 data = ReadRegister(BMP280_REGISTER_CHIPID);
            byte chipId = (byte)(data & 0xFF); // Only first received byte contains chip id

            await Task.Delay(1);

            if (chipId == BMP280_SIGNATURE)
            {
                Debug.WriteLine("Signature of BMP280 device matches.");
                return true;
            }
            else
            {
                Debug.WriteLine("Signature mismatch. Expected 0x58, but retreived  0x{0:X2}.", chipId);
            }               

            return false;
        }

        private async Task<BMP280CalibrationData> ReadCoefficeints()
        {
            Debug.WriteLine("BMP280DigitalPressureSensor::ReadCoefficients");

            // 16 bit calibration data is stored as Little Endian, the helper method will do the byte swap.
            BMP280CalibrationData calibrationData = new BMP280CalibrationData();

            // Read temperature calibration data
            calibrationData.dig_T1 = ReadRegister(BMP280_REGISTER_DIG_T1);
            calibrationData.dig_T2 = (Int16)ReadRegister(BMP280_REGISTER_DIG_T2);
            calibrationData.dig_T3 = (Int16)ReadRegister(BMP280_REGISTER_DIG_T3);

            // Read presure calibration data
            calibrationData.dig_P1 = ReadRegister(BMP280_REGISTER_DIG_P1);
            calibrationData.dig_P2 = (Int16)ReadRegister(BMP280_REGISTER_DIG_P2);
            calibrationData.dig_P3 = (Int16)ReadRegister(BMP280_REGISTER_DIG_P3);
            calibrationData.dig_P4 = (Int16)ReadRegister(BMP280_REGISTER_DIG_P4);
            calibrationData.dig_P5 = (Int16)ReadRegister(BMP280_REGISTER_DIG_P5);
            calibrationData.dig_P6 = (Int16)ReadRegister(BMP280_REGISTER_DIG_P6);
            calibrationData.dig_P7 = (Int16)ReadRegister(BMP280_REGISTER_DIG_P7);
            calibrationData.dig_P8 = (Int16)ReadRegister(BMP280_REGISTER_DIG_P8);
            calibrationData.dig_P9 = (Int16)ReadRegister(BMP280_REGISTER_DIG_P9);

            await Task.Delay(1);
            return calibrationData;
        }

        private double CompensateMeasuredTemperature(Int32 data)
        {
            Debug.WriteLine("BMP280DigitalPressureSensor::CompensateMeasuredTemperature");

            double var1, var2, result;

            //The temperature is calculated using the compensation formula in the BMP280 datasheet
            var1 = ((data / 16384.0) - (coefficeints.dig_T1 / 1024.0)) * coefficeints.dig_T2;
            var2 = ((data / 131072.0) - (coefficeints.dig_T1 / 8192.0)) * coefficeints.dig_T3;

            fineTemperature = (Int32)(var1 + var2);

            result = (var1 + var2) / 5120.0;
            return result;
        }

        public async Task<double> GetTemperatureOnce(BMP280Oversampling samplingRate)
        {
            Debug.WriteLine("BMP280DigitalPressureSensor::GetTemperatureOnce");

            double result = 0.0;

            if (!isInitialized) await Initialize();

            // Save current control register state
            UInt16 data = ReadRegister(BMP280_REGISTER_CONTROL);
            byte control = (byte)(data & 0xFF); // low byte

            // Set control register to force mode and supplied sampling rate
            byte temporaryControl = (byte)((byte)BMP280PowerMode.Forced + ((byte)samplingRate << 5));
            WriteRegister((byte)BMP280_REGISTER_CONTROL, temporaryControl);
            await Task.Delay(1);

            // Read once (force mode) the mesuared temperature
            data = ReadRegister(BMP280_REGISTER_TEMPDATA_MSB);
            byte tmsb = (byte)(data & 0xFF);
            byte tlsb = (byte)(data >> 8);

            data = ReadRegister(BMP280_REGISTER_TEMPDATA_XLSB);
            byte txlsb = (byte)(data & 0xFF);

            Int32 t = (tmsb << 12) + (tlsb << 4) + (txlsb >> 4);

            // Convert the raw value to the temperature in degC
            result = CompensateMeasuredTemperature(t);

            // Restore control register
            //
            // Important note:
            // ---------------
            // Resetting force mode has no affect ! Only if mode was previously set
            // to "normal" (cycling) this reset operation makes sense. In force mode
            // the chip do a measurement and returns back to sleep mode.
            WriteRegister((byte)BMP280_REGISTER_CONTROL, control);
            await Task.Delay(1);

            return result;
        }

        private static string GetByteBinaryString(byte n)
        {
            char[] b = new char[8];
            int pos = 7;
            int i = 0;

            while (i < 8)
            {
                if ((n & (1 << i)) != 0)
                {
                    b[pos] = '1';
                }
                else
                {
                    b[pos] = '0';
                }
                pos--;
                i++;
            }

            return new string(b);
        }
    }
}
