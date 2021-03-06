﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

namespace DiscTools.Inspection
{
    public partial class Interrogator
    {
        public bool ScanISOPSX()
        {
            if (discI.Data._ISOData.ApplicationIdentifier == "PLAYSTATION")
            {      
                // store lba for SYSTEM.CNF
                KeyValuePair<string, ISO.ISONode> cnf = discI.Data._ISOData.ISOFiles.Where(a => a.Key.Contains("SYSTEM.CNF")).FirstOrDefault();
                if (cnf.Key != null && cnf.Key.Contains("SYSTEM.CNF"))
                {
                    ifn = cnf.Value;
                    CurrentLBA = Convert.ToInt32(ifn.Offset);
                }
                else
                {
                    // assume LBA 23
                    CurrentLBA = 23;
                }

                if (GetPSXData())
                    return true;

                // some jap discs (thunder storm and road blaster) appear not to even have a SYSTEM.CNF
                // detect whether PSX.EXE exists, and if so try and parse this first
                KeyValuePair<string, ISO.ISONode> psx = discI.Data._ISOData.ISOFiles.Where(a => a.Key.Contains("PSX.EXE")).FirstOrDefault();
                if (psx.Key != null && psx.Key.Contains("PSX.EXE"))
                {
                    ifn = psx.Value;
                    CurrentLBA = Convert.ToInt32(ifn.Offset);

                    byte[] data = di.ReadData(CurrentLBA, 2048);
                    byte[] data32 = data.ToList().ToArray();

                    string sS = System.Text.Encoding.Default.GetString(data32);

                    if (sS.Contains("Sony Computer Entertainment Inc. for Japan"))
                    {
                        // it is PSX - try and get the serial - may need to seek forward a bit
                        for (int i = CurrentLBA; i < CurrentLBA + ifn.Length; i++)
                        {
                            byte[] d = di.ReadData(i, 2048);
                            string s = System.Text.Encoding.Default.GetString(d);

                            if (s.ToUpper().Contains("SLPS"))
                            {
                                int ind = s.IndexOf("SLPS");
                                char[] serialChars = s.Substring(s.IndexOf("SLPS")).Take(10).ToArray();
                                string serial = new string(serialChars).Trim();
                                discI.Data.SerialNumber = serial;
                                discI.Data.GameTitle = discI.Data._ISOData.VolumeIdentifier;
                                discI.Data.Publisher = discI.Data._ISOData.PublisherIdentifier;
                                discI.Data.Developer = discI.Data._ISOData.DataPreparerIdentifier;
                                discI.Data.AreaCodes = "JAPAN";
                                break;
                            }
                        }
                        DiscSubType = DetectedDiscType.SonyPSX;
                        return true;
                    }

                    //if (GetPSXData())
                        return true;
                }
            }

            return false;
        }
        
        public bool GetPSXData()
        {
            byte[] data = di.GetPSXSerialNumber(CurrentLBA);
            byte[] data32 = data.ToList().ToArray();

            string sS = System.Text.Encoding.Default.GetString(data32);

            return GetPSXData(sS);
        }

        public bool GetPSXData(string lbaString)
        {
            if (lbaString.Contains("cdrom:"))
            {
                /*
             * regex pattern for PSX serial extraction
             * supplied by clobber @ OpenEmu:
             * https://github.com/OpenEmu/OpenEmu/blob/master/OpenEmu/PlayStation/OEPSXSystemController.m#L157-L167
                // RegEx pattern match the disc serial (Note: regex backslashes are escaped)
                // Handles all cases I've encountered so far:
                //  BOOT=cdrom:\SCES_015.64;1           (no whitespace)
                //  BOOT=cdrom:\SLUS_004.49             (no semicolon)
                //  BOOT=cdrom:\SLUS-000.05;1           (hyphen instead of underscore)
                //  BOOT = cdrom:\SLES_025.37;1         (whitespace)
                //  BOOT = cdrom:SLUS_000.67;1          (no backslash)
                //  BOOT = cdrom:\slus_005.94;1         (lowercase)
                //  BOOT = cdrom:\TEKKEN3\SLUS_004.02;1 (extra path)
                //  BOOT	= cdrom:\SLUS_010.41;1      (horizontal tab)
            */
                string PSXSerialRegex = @"BOOT\s*=\s*?cdrom:\\?(.+\\)?(.+?(?=;|\s))";

                Regex pattern = new Regex(PSXSerialRegex);
                var match = pattern.Match(lbaString);
                int mCount = match.Groups.Count;

                if (mCount == 3)
                {
                    discI.Data.SerialNumber = match.Groups[2].ToString().Replace("_", "-").Replace(".", "");
                    DiscSubType = DetectedDiscType.SonyPSX;

                    if (isIso)
                    {
                        discI.Data.GameTitle = discI.Data._ISOData.VolumeIdentifier;
                        discI.Data.Publisher = discI.Data._ISOData.PublisherIdentifier;
                        discI.Data.Developer = discI.Data._ISOData.DataPreparerIdentifier;
                    }

                    return true;
                }
            }

            if (lbaString.Contains("BOOT2"))
            {
                // PS2
                string PS2SerialRegex = @"BOOT2\s*=\s*?cdrom0:\\?(.+\\)?(.+?(?=;|\s))";

                Regex pattern = new Regex(PS2SerialRegex);
                var match = pattern.Match(lbaString);
                int mCount = match.Groups.Count;

                if (mCount == 3)
                {
                    discI.Data.SerialNumber = match.Groups[2].ToString().Replace("_", "-").Replace(".", "");
                    DiscSubType = DetectedDiscType.SonyPS2;

                    if (isIso)
                    {
                        discI.Data.GameTitle = discI.Data._ISOData.VolumeIdentifier;
                        discI.Data.Publisher = discI.Data._ISOData.PublisherIdentifier;
                        discI.Data.Developer = discI.Data._ISOData.DataPreparerIdentifier;
                    }

                    // get other info
                    string[] arr = lbaString.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    string version = arr[1];
                    string region = arr[2];

                    discI.Data.Version = version.Replace("VER = ", "").Trim().TrimEnd('\0');
                    discI.Data.AreaCodes = region.Replace("VMODE = ", "").Trim().TrimEnd('\0');


                    return true;
                }
            }

            

            

            return false;
        }
    }
}
