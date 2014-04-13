using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using SPQR;
using MySPQR;
using MySPQR.Classes;
using System.Diagnostics;
using System.Runtime.InteropServices;


//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// nrgdret rotation                                                                                             //
// v[alpha0.1]                                                                                                  //
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Notes:                                                                                                       //
//                                                                                                              //
// - This is ALPHA! Don't expect to work flawlessly and to achive an amazing dps! I've just released it to get  //
//   some feedback to help me improve it, please keep that in mind when posting bugs!                           //
//                                                                                                              //
// - Inquisition is tracked by using timers... this is far for perfect and right now the OnStop() method        //
//   is not being called by SPQR which means that the timer won't stop when you stop the rotation. I would      //
//   recomend to restart SPQR if you have stopped the rotation. Hopefully an aura remaining time method         //
//   will be provided in a new version or the OnStop() method will be fixed to allow using timers meanwhile.    //
//                                                                                                              //
// - You can switch between single/aoe rotations by pressing 'z' key. Once again this is not perfect because    //
//   we can't hook to keypresses but instead have to use pooling, so you keypress can be missed. I've added     //
//   a beep for single and 3 beeps for aoe, so you can know the keypress has been detected and which one is     //
//   the active one. Maybe in a new SPQR version the program allows us to detect keypresses by handling events! //
//                                                                                                              //
// - Rotation is based on icy-veins:                                                                            //
//   [http://www.icy-veins.com/retribution-paladin-wow-pve-dps-rotation-cooldowns-abilities]                    //
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Upcoming:
// - Change rotation when not in melee range, cast only ranged spells.
// - Detect Divine Purpose procs.
// - Detect 4 pieces tier procs.
// - Pause rotation button.

namespace SPQR.Engine
{
    class Paladin : Engine.FightModule
    {
        #region private vars
            private System.Timers.Timer inqTracker= new System.Timers.Timer(58000);
            private bool inqUp=false;
            bool isAOE;
            MySPQR.Classes.WoWLocalPlayer ME;
        #endregion

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public override string DisplayName
        {
            get { return "nrgdRET"; }                      //This is the name displayed in SPQR's Class selection DropdownList
        }

        #region enums
        internal enum Spells : int                      //This is a convenient list of all spells used by our combat routine
        {                                               //you can have search on wowhead.com for spell name, and get the id in url
            Inquisition = 84963,
            CrusaderStrike = 35395,
            Exorcism = 879,
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
            AvengingWrath=31884,
        }
        #endregion

        #region singleRotation
        private void castNextSpellbySinglePriority()
        {
            var TARGET = MySPQR.Internals.ObjectManager.Target;
			
            if (ME.HolyPower > 2 && !inqUp)
            {
                if (MySPQR.Internals.ActionBar.CanCast((int)Spells.Inquisition))
                {
                    MySPQR.Internals.ActionBar.GetSlotById((int)Spells.Inquisition).Execute();
                    inqTracker.Start();
                    inqUp = true;
                }
            }
            if (ME.HolyPower == 5)
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.TemplarsVerdict).Execute();
            }
            if (((TARGET.HealthPercent < 20 ) || avengingWrathActive()) && MySPQR.Internals.ActionBar.CanCast((int)Spells.HammerOfWrath))
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.HammerOfWrath).Execute();
            }
            if (MySPQR.Internals.ActionBar.CanCast((int)Spells.Exorcism))
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.Exorcism).Execute();
            }
            if (MySPQR.Internals.ActionBar.CanCast((int)Spells.CrusaderStrike))
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.CrusaderStrike).Execute();
            }
            if (MySPQR.Internals.ActionBar.CanCast((int)Spells.Judgement))
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.Judgement).Execute();
            }
            if (ME.HolyPower >= 3)
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.TemplarsVerdict).Execute();
            }
        }
        #endregion

        #region AOE rotation
        private void castNextSpellbyAOEPriority()
        {
            var TARGET = MySPQR.Internals.ObjectManager.Target;

            if (ME.HolyPower > 2 && !inqUp)
            {
                if (MySPQR.Internals.ActionBar.CanCast((int)Spells.Inquisition))
                {
                    MySPQR.Internals.ActionBar.GetSlotById((int)Spells.Inquisition).Execute();
                    inqTracker.Start();
                    inqUp = true;
                }
            }
            if (ME.HolyPower == 5)
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.DivineStorm).Execute();
            }
            if (((TARGET.HealthPercent < 20) || avengingWrathActive()) && MySPQR.Internals.ActionBar.CanCast((int)Spells.HammerOfWrath))
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.HammerOfWrath).Execute();
            }
            if (MySPQR.Internals.ActionBar.CanCast((int)Spells.Exorcism))
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.Exorcism).Execute();
            }
            if (MySPQR.Internals.ActionBar.CanCast((int)Spells.HammeroftheRighteous))
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.HammeroftheRighteous).Execute();
            }
            if (MySPQR.Internals.ActionBar.CanCast((int)Spells.Judgement))
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.Judgement).Execute();
            }
            if (ME.HolyPower >= 3)
            {
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.DivineStorm).Execute();
            }
        }
        #endregion

        #region auxFunctions
        private bool avengingWrathActive()
        {
            return MySPQR.Internals.ObjectManager.WoWLocalPlayer.HasAurabyId((int)Auras.AvengingWrath);
            // Linq not working?
            //return MySPQR.Internals.ObjectManager.WoWLocalPlayer.AuraList.Any(aura => aura.Id == (int)Auras.AvengingWrath);
        }

        public void changeRotation() 
        {
            if (isAOE)
            {
                Console.Beep(5000, 100);
                isAOE = false;
                while (MySPQR.Internals.CooldownManager.GCDActive) { }
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.SealofTruth).Execute();
                SPQR.Logger.WriteLine("Rotation Single!!");
            }
            else
            {
                Console.Beep(5000, 100);
                Console.Beep(5000, 100);
                Console.Beep(5000, 100);
                isAOE = true;
                while (MySPQR.Internals.CooldownManager.GCDActive) { }
                MySPQR.Internals.ActionBar.GetSlotById((int)Spells.SealofRighteousness).Execute();
                SPQR.Logger.WriteLine("Rotation AOE!!");
            }
        }
        #endregion

        public override void CombatLogic()              //This is the DPS / healing coutine, called in loop by SPQR all code here is executed
        {
            if (!MySPQR.Internals.CooldownManager.GCDActive && MySPQR.Internals.ObjectManager.Target.IsValid)
            {
                if (isAOE) { castNextSpellbyAOEPriority(); } else { castNextSpellbySinglePriority(); }
            }
            if ((GetAsyncKeyState(90) == -32767))
            {
                changeRotation();
            }
        }

        public override void OnLoad()   //This is called when the Customclass is loaded in SPQR
        {
            SPQR.Logger.WriteLine("CustomClass " + DisplayName + " Loaded");
        }

        public override void OnClose() //This is called when the Customclass is unloaded in SPQR
        {
            inqTracker.Stop();
            SPQR.Logger.WriteLine("CustomClass " + DisplayName + " Unloaded, Goodbye !");
        }

        public override void OnStart() //This is called once, when you hit CTRL+X to start SPQR combat routine
        {
            SPQR.Logger.WriteLine("Launching " + DisplayName + " routine... enjoy! Press z to switch between single/aoe");
            ME = MySPQR.Internals.ObjectManager.WoWLocalPlayer;
            inqTracker.Elapsed += new System.Timers.ElapsedEventHandler(inqTracker_Elapsed);
        }

        private void inqTracker_Elapsed(object source, ElapsedEventArgs e)
        {
            inqUp = false;
            SPQR.Logger.WriteLine("DEBUG: Timer UP!");
        }

        public override void OnStop() //This is called once, when you hit CTRL+X to stop SPQR combat routine
        {
            inqTracker.Stop();
            SPQR.Logger.WriteLine("Stopping " + DisplayName + " routine... gl smashing keys.");
        }

    }

}