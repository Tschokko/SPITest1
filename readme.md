# SPITest1
This repository includes a C# Universal Windows application for ARM running on a Raspberry Pi 2 board 
with Windows 10 IoT Core and selected Adafruit parts of the Windows 10 IoT starter kit.
This project should help to learn programming devices thru SPI. I started with the BMP280 pressure and
temperature sensor. Connect the sensor to all relevant pins of SPI0 and to chip select pin 0 (CE0).

Intial commit on 03-06-2016
---------------------------
The current application is totally dump. It read once (force mode) the temperature from a BMP280 sensor. 
I created a seperate class for all the BMP280 logic. Inside Main Application I create a variable of type
BMP280DigitalPressureSensor, initialize it properly and read the current temperature.

