--------------------------------------------------------------------
-- Project    : MERLIN
-- @file      : adc_128S102_model.vhd
-- Language   : VHDL-93
-- @brief     : Simulate the ADC. NON SYNTHESIZABLE
-- @copyright : SpaceTech GmbH, Germany
--------------------------------------------------------------------
-- Last change: KS 18.08.2023 17:44:58
-- Last checkin $Rev: 353 $ $Date:: 2016-08-29 18:26:21#$
-- Revision list
-- Version Author Date      Changes
-- 1.0            dd-mm-yy  initial version
--------------------------------------------------------------------
LIBRARY IEEE;
USE IEEE.STD_LOGIC_1164.ALL;
USE IEEE.NUMERIC_STD.ALL;
USE work.functions_pkg.ALL;
USE work.architecture_pkg.ALL;

ENTITY adc_128S102_model IS PORT (
   sclk              : IN  STD_LOGIC;
   cs_n              : IN  STD_LOGIC;
   din               : IN  STD_LOGIC;
   analog_values     : IN  adc_values;   -- Analog values to be converted
   dout              : OUT STD_LOGIC
);
END adc_128S102_model;

ARCHITECTURE rtl OF adc_128S102_model IS

SIGNAL channel   : NATURAL RANGE 0 TO 7;
SIGNAL data      : UNSIGNED(2 DOWNTO 0);
SIGNAL count     : NATURAL RANGE 0 TO 16;
SIGNAL shifter   : UNSIGNED(11 DOWNTO 0);

BEGIN

dout <= shifter(11);

send_data_proc : PROCESS(sclk, cs_n)
BEGIN
   IF cs_n = '1' THEN
      count <= 0;
      shifter <= (OTHERS => '0');
   ELSIF  falling_edge(sclk) OR (sclk = '0' AND falling_edge(cs_n))  THEN
      shifter <= shifter(10 DOWNTO 0) & '0';
      IF  count = 4  THEN
         shifter <= analog_values(channel);
      END IF;
      IF count = 16 THEN
         count <= 1;
      ELSE
         count <= count + 1;
      END IF;
   END IF;
END PROCESS send_data_proc;

next_channel_proc : PROCESS (sclk)
BEGIN
   IF  rising_edge(sclk)  THEN
      IF count = 6 THEN
         channel <= to_integer(data);
      ELSIF count = 3 THEN
         data(2) <= din;
      ELSIF count = 4  THEN
         data(1) <= din;
      ELSIF count = 5  THEN
         data(0) <= din;
      END IF ;
   END IF;
END PROCESS next_channel_proc;

END rtl;
