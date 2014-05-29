using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using Anthrax.WoW.Classes.ObjectManager;
using Anthrax.WoW.Internals;
using Anthrax.AI.Controllers;
using Anthrax.WoW;
using Anthrax;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections;
using System.ComponentModel;
using System.Threading;
using System.Xml.Serialization;
using System.ComponentModel;
using System.IO;


//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// nrgdret rotation                                                                                             //
// v[alpha0.3]                                                                                                  //
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Notes:                                                                                                       //
//                                                                                                              //
// - This is ALPHA! Don't expect to work flawlessly and to achive an amazing dps! I've just released it to get  //
//   some feedback to help me improve it, please keep that in mind when posting bugs!                           //
//                                                                                                              //
// - You can switch between single/aoe rotations by pressing 'z' key. Once again this is not perfect because    //
//   we can't hook to keypresses but instead have to use pooling, so you keypress can be missed. I've added     //
//   OSD so you can know which rotation is active.                                                              //
//                                                                                                              //
// - Rotation is based on icy-veins:                                                                            //
//   [http://www.icy-veins.com/retribution-paladin-wow-pve-dps-rotation-cooldowns-abilities]                    //
//                                                                                                              //
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Upcoming:
// - Change rotation when not in melee range, cast only ranged spells.
// - Add AOE rotation for 2-3 mobs.
// - Add some sort of config (OSD placement & colors, T16P4 on SP, ... )
// - Pause rotation button.
//
// Changelog:
// ..:: v[alpha0.3] ::..
//      - Migrated to Anthrax bot API
//      - Removed OSD until properly tested with Anthrax
//      - Added basic settings
// ..:: v[alpha0.2] ::..
//      - Added OSD (On Screen Display) to show selected rotation.
//      - Removed inq timer, now works with timeleft, should always refresh inq correctly.
//      - Detecting Divine Purpose procs, talent is viable for this rotation.
//      - Detect 4 pieces tier procs (WARNING: Single rotation will cast DS when tier procs!)
//
// Known Bugs:
//      - Starting rotation before logging in may cause rotation to bug, restart SPQR

namespace Anthrax
{
    class Paladin : Modules.ICombat
    {
        #region private vars
        bool isAOE;
        WowLocalPlayer ME;
        Settings CCSettings = new Settings();
        //Stopwatch stopwatch;
        //List<long> averageScanTimes;
        #endregion

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public override string Name
        {
            get { return "nrgdRET"; }
        }

        #region enums
        internal enum Spells : int                      //This is a convenient list of all spells used by our combat routine
        {                                               //you can have search on wowhead.com for spell name, and get the id in url
            Inquisition = 84963,
            CrusaderStrike = 35395,
            Exorcism = 879,
            MassExorcism = 122032,
            HolyShock = 20473,
            Judgement = 20271,
            AvengersShield = 31935,
            TemplarsVerdict = 85256,
            ShieldOfRighteous = 53600,
            ExecutionSentence = 114157,
            LightsHammer = 114158,
            HolyPrism = 114165,
            HammerOfWrath = 24275,
            SealOfInsight = 20165,
            Rebuke = 96231,
            DivineStorm = 53385,
            HammeroftheRighteous = 53595,
            SealofTruth = 31801,
            SealofRighteousness = 20154
        }

        internal enum Auras : int                       //This is another convenient list of Auras used in our combat routine
        {												//you can have those in wowhead.com (again) and get the id in url
            SealOfInsight = 20165,
            Inquisition = 84963,
            AvengingWrath = 31884,
            DivinePurpose = 90174,
            T164PB = 144595,
        }
        #endregion

        #region singleRotation
        private void castNextSpellbySinglePriority(WowUnit TARGET)
        {
            // Vars
            int inqTimeLeft;
            if (ME.HasAuraById((int)Spells.Inquisition))
            {
                inqTimeLeft = ME.Auras.Where(a => a.SpellId == (int)Spells.Inquisition).First().TimeLeft;
            }
            else
            {
                inqTimeLeft = 3;
            }
            int hp = ME.GetPower(WowUnit.WowPowerType.HolyPower); // may change during execution, seems more efficient this way

            if (inqTimeLeft <= CCSettings.RefreshInquisition)
            {
                if (hp > 2 || inqTimeLeft <= CCSettings.ForceRefreshInquisition)
                {
                    if (Spell.CanCast((int)Spells.Inquisition))
                    {
                        Logger.WriteLine("Casting Inq");
                        ActionBar.ExecuteSpell((int)Spells.Inquisition);
                    }
                }
            }
            if (CCSettings.HasT164PB && ME.HasAuraById((int)Auras.T164PB))
            {
                ActionBar.ExecuteSpell((int)Spells.DivineStorm);
            }
            if (ME.HasAuraById((int)Auras.DivinePurpose) || hp == 5)
            {
                ActionBar.ExecuteSpell((int)Spells.TemplarsVerdict);
            }
            if (((TARGET.HealthPercent < 20) || avengingWrathActive()) && Spell.CanCast((int)Spells.HammerOfWrath))
            {
                ActionBar.ExecuteSpell((int)Spells.HammerOfWrath);
            }
            if (Spell.CanCast((int)Spells.Exorcism))
            {
                ActionBar.ExecuteSpell((int)Spells.Exorcism);
            }
            if (Spell.CanCast((int)Spells.MassExorcism))
            {
                ActionBar.ExecuteSpell((int)Spells.MassExorcism);
            }
            if (Spell.CanCast((int)Spells.CrusaderStrike))
            {
                ActionBar.ExecuteSpell((int)Spells.CrusaderStrike);
            }
            if (Spell.CanCast((int)Spells.Judgement))
            {
                ActionBar.ExecuteSpell((int)Spells.Judgement);
            }
            if (hp >= 3)
            {
                ActionBar.ExecuteSpell((int)Spells.TemplarsVerdict);
            }
        }
        #endregion

        #region AOE>4 rotation
        private void castNextSpellbyAOEPriority(WowUnit TARGET)
        {
            // Vars
            int inqTimeLeft;
            if (ME.HasAuraById((int)Spells.Inquisition))
            {
                inqTimeLeft = ME.Auras.Where(a => a.SpellId == (int)Spells.Inquisition).First().TimeLeft;
            }
            else
            {
                inqTimeLeft = 3;
            }
            int hp = ME.GetPower(WowUnit.WowPowerType.HolyPower); // may change during execution, seems more efficient this way

            if (inqTimeLeft <= CCSettings.RefreshInquisition)
            {
                if (hp > 2 || inqTimeLeft <= CCSettings.ForceRefreshInquisition)
                {
                    if (Spell.CanCast((int)Spells.Inquisition))
                    {
                        ActionBar.ExecuteSpell((int)Spells.Inquisition);
                    }
                }
            }
            if ((CCSettings.HasT164PB && ME.HasAuraById((int)Auras.T164PB)) || ME.HasAuraById((int)Auras.DivinePurpose) || hp == 5)
            {
                ActionBar.ExecuteSpell((int)Spells.DivineStorm);
            }
            if (((TARGET.HealthPercent < 20) || avengingWrathActive()) && Spell.CanCast((int)Spells.HammerOfWrath))
            {
                ActionBar.ExecuteSpell((int)Spells.HammerOfWrath);
            }
            if (Spell.CanCast((int)Spells.Exorcism))
            {
                ActionBar.ExecuteSpell((int)Spells.Exorcism);
            }
            if (Spell.CanCast((int)Spells.MassExorcism))
            {
                ActionBar.ExecuteSpell((int)Spells.MassExorcism);
            }
            if (Spell.CanCast((int)Spells.HammeroftheRighteous))
            {
                ActionBar.ExecuteSpell((int)Spells.HammeroftheRighteous);
            }
            if (Spell.CanCast((int)Spells.Judgement))
            {
                ActionBar.ExecuteSpell((int)Spells.Judgement);
            }
            if (hp >= 3)
            {
                ActionBar.ExecuteSpell((int)Spells.DivineStorm);
            }
        }
        #endregion

        #region auxFunctions
        private bool avengingWrathActive()
        {
            return ME.HasAuraById((int)Auras.AvengingWrath);
        }

        public void changeRotation()
        {
            if (isAOE)
            {
                Console.Beep(5000, 100);
                isAOE = false;
                while (!ME.HasAuraById((int)Spells.SealofTruth)) { ActionBar.ExecuteSpell((int)Spells.SealofTruth); }
                Logger.WriteLine("Rotation Single!!");
            }
            else
            {
                Console.Beep(5000, 100);
                Console.Beep(5000, 100);
                Console.Beep(5000, 100);
                isAOE = true;
                while (!ME.HasAuraById((int)Spells.SealofRighteousness)) { ActionBar.ExecuteSpell((int)Spells.SealofRighteousness); }
                Logger.WriteLine("Rotation AOE!!");
            }
        }
        #endregion

        public override void OnCombat(WowUnit TARGET)
        {
            /* Performance tests
            stopwatch.Stop();
            averageScanTimes.Add(stopwatch.ElapsedMilliseconds);
            SPQR.Logger.WriteLine("Elapsed:  " + stopwatch.ElapsedMilliseconds.ToString() + " miliseconds, average:" + (averageScanTimes.Sum() / averageScanTimes.Count()).ToString() + ",Max:" + averageScanTimes.Max());
            stopwatch.Restart();
             */
            if (ME.InCombat) 
            {
                if (!Cooldown.IsGlobalCooldownActive && TARGET.IsValid)
                {
                    if (isAOE) { castNextSpellbyAOEPriority(TARGET); } else { castNextSpellbySinglePriority(TARGET); }
                }
            }
            else
            {

            }
            if ((GetAsyncKeyState(90) == -32767))
            {
                changeRotation();
            }
        }

        public override void OnLoad()   //This is called when the Customclass is loaded in SPQR
        {
            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(Settings));

                using (StreamReader rd = new StreamReader(AppDomain.CurrentDomain.BaseDirectory + "\\Combats\\nrgdRET.xml"))
                {
                    CCSettings = xs.Deserialize(rd) as Settings;
                }
            }
            catch
            {

            }
            finally
            {
                if (CCSettings == null)
                    CCSettings = new Settings();
            }
            Logger.WriteLine("CustomClass " + Name + " Loaded");
        }

        public override void OnUnload() //This is called when the Customclass is unloaded in SPQR
        {
            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(Settings));
                using (StreamWriter wr = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "\\Combats\\nrgdRET.xml"))
                {
                    xs.Serialize(wr, CCSettings);
                }
            }
            catch { }
            Logger.WriteLine("CustomClass " + Name + " Unloaded, Goodbye !");
        }

        public override void OnBotStart() //This is called once, when you hit CTRL+X to start SPQR combat routine
        {
            ME = ObjectManager.LocalPlayer;
            Logger.WriteLine("Launching " + Name + " routine... enjoy! Press z to switch between single/aoe");
            /* Performance tests
            stopwatch=new Stopwatch();
            averageScanTimes = new List<long>();
             */
        }

        public override void OnBotStop() //This is called once, when you hit CTRL+X to stop SPQR combat routine
        {
            Logger.WriteLine("Stopping " + Name + " routine... gl smashing keys.");
        }

        public override object SettingsProperty
        {
            get
            {
                return CCSettings;
            }
        }

        public override void OnPatrol() { }

        public override void OnPull(WoW.Classes.ObjectManager.WowUnit unit) { }

    }

    [Serializable]
    public class Settings
    {
        public int RefreshInquisition = 4;
        public int ForceRefreshInquisition = 1;
        public bool HasT164PB = true;

        [XmlIgnore]
        [CategoryAttribute("Global Settings"),
        DisplayName("T16 4p bonus?"), DefaultValueAttribute(true)]
        public bool _HasT164PB
        {
            get
            {
                return HasT164PB;
            }
            set
            {
                HasT164PB = value;
            }
        }

        [XmlIgnore]
        [CategoryAttribute("Inquisition Refreshing"),
        DisplayName("Try to refresh below"), DefaultValueAttribute(4)]
        public int _RefreshInquisition
        {
            get
            {
                return RefreshInquisition;
            }
            set
            {
                RefreshInquisition = value;
            }
        }

        [XmlIgnore]
        [CategoryAttribute("Inquisition Refreshing"),
        DisplayName("Force refresh below"), DefaultValueAttribute(1)]
        public int _ForceRefreshInquisition
        {
            get
            {
                return ForceRefreshInquisition;
            }
            set
            {
                ForceRefreshInquisition = value;
            }
        }

    }
}