#include "hw_driver.h"
//#include "socket.h"
//#include "http_parser.h"
#include "rtc.h"
//#include "RFM69registers.h"
//#include "RFM69.h"
//#include "u8g2.h"
//#include "displaySupport.h"
#include "usb_hid.h"
#include "stdint.h"
#include "stdbool.h"
#include "ff.h"
#include "mem25lXX.h"
//#include "bms_ina22x.h"


ramIDS spiFlash;

 
char debugLine[64];


rtc_date sys_rtc = {
	.date = 4,
	.month = 12,
	.year = 2023,
	.dayofweek = 1,
	.hour = 23,
	.minute = 54,
	.second = 00
};
uint8_t timeSyncRequest = 0;




static uint8_t hid_generic_in_report[64];
static uint8_t hid_generic_out_report[64];
uint8_t printDataReady = 0;

uint8_t labelData[1024][8] = {0};
uint8_t emptuWord[8] = {0};
uint16_t pixToPrint = 0;
uint8_t rollAfterPrint = 0;
static bool usb_device_cb_generic_out(const uint8_t ep, const enum usb_xfer_code rc, const uint32_t count)
{	
	hiddf_generic_read(hid_generic_out_report, sizeof(hid_generic_out_report));
	if (hid_generic_out_report[0] == 164) {
		pixToPrint = hid_generic_out_report[5];
		pixToPrint = (pixToPrint << 8) + hid_generic_out_report[6];
		printDataReady = hid_generic_out_report[3];
		rollAfterPrint = hid_generic_out_report[1];
	}
	if (hid_generic_out_report[0] == 161 || hid_generic_out_report[0] == 162 || hid_generic_out_report[0] == 163) {
		uint16_t blockIndex = hid_generic_out_report[2];
		blockIndex = (blockIndex << 8) + hid_generic_out_report[3];
		if (blockIndex < 512) {
			memcpy(labelData[blockIndex], &hid_generic_out_report[5], 8);
		}
	}
	return true;
}

static bool usb_device_cb_generic_in(const uint8_t ep, const enum usb_xfer_code rc, const uint32_t count)
{
	return true;
}




int main(void)
{
	//atmel_start_init();
	sys_hw_init();


	
	
	
	
	
	chipGetIDS(&spiFlash);
	
	
	usb_HID_init();
	hiddf_generic_register_callback(HIDDF_GENERIC_CB_READ, (FUNC_PTR)usb_device_cb_generic_out);
	//hiddf_generic_register_callback(HIDDF_GENERIC_CB_WRITE, (FUNC_PTR)usb_device_cb_generic_in);
	hiddf_generic_read(hid_generic_out_report, sizeof(hid_generic_out_report));
	
	//PB16 - MOSI
	//PB17 - CLK
	//PA15 - TH_EXPO
	//PA19 - TH_LATCH
	//PA14 - MOTOR
	
	while (1) {
		
		
		
		delay_ms(1);
		//gpio_toggle_pin_level(TH_MOT);
		
		
		if(printDataReady){
			gpio_set_pin_level(GLD, true);
			gpio_set_pin_level(TH_MOT, true);
			delay_ms(150);
			printDataReady=0;
			for(uint16_t i = 0; i<pixToPrint; i++){
				
				ETH_SPI_WriteBuff(&labelData[i], sizeof(labelData[i]));
				gpio_set_pin_level(TH_LATCH, false);
				delay_us(1);
				gpio_set_pin_level(TH_LATCH, true);
			
				//
				gpio_set_pin_level(TH_EXPO, true);
				
				
				delay_us(2500);
				
				ETH_SPI_WriteBuff(&emptuWord, sizeof(emptuWord));
				gpio_set_pin_level(TH_LATCH, false);
				delay_us(10);
				gpio_set_pin_level(TH_LATCH, true);
				
				gpio_set_pin_level(TH_EXPO, false);
				delay_us(4000);
				
			}
			if(rollAfterPrint != 0){
				delay_ms(1000);
			}
			
			gpio_set_pin_level(TH_MOT, false);
			gpio_set_pin_level(GLD, false);
			
			
		}
		
		
		if (RTC_IRQ_Ready())
		{
			gpio_toggle_pin_level(RLD);
			rtc_sync(&sys_rtc);
			//sprintf(rtcData, "%02d:%02d:%02d", sys_rtc.hour, sys_rtc.minute, sys_rtc.second);
		}
		
	}
}
