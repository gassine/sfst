using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using CitizenFX.Core.UI;
using FivePD.API;
using FivePD.API.Utils;
//using NativeUI;
using MenuAPI;

namespace sfst
{

    internal class sfstPlugin : Plugin
    {
        public class DRUNK_LEVEL
        {
            public static int SOBER = 0;
            public static int SLIGHTLY_DRUNK = 1;
            public static int DRUNK = 2;
            public static int VERY_DRUNK = 3;
        }

        public class TEST_OPTIONS
        {
            public static int CLEAR = 0;
            public static int ONE_LEG_STAND = 1;
            public static int NYSTAGMUS = 2;
            public static int WALK_AND_TURN = 3;
        }

        private Menu sfstMenu;
        private Ped tsDriver = null;
        private Vehicle tsVehicle = null;
        private Ped player = null;

        // This is just the constructor of what will happen when the script starts
        internal sfstPlugin()
        {
            AddSfstOptions();
            registerSfstMenu();
            // NOTE: Add an option to do an observation check that will tell information about the suspect
            // NOTE: Add another option to do a smell check that will check if the window is open to get smell information
            // NOTE: Add an option to ask the ped to open the window
            // NOTE: Add a submenu to SHOW how the test must be done so the animations display on the player
            // NOTE: Add a section for consent questions
            // Evaluate modifying FivePD database to add data to it that can be consulted later on

            // Below only needed if we need to hardcode execution to keybind
            //Tick += checkIfMenuKeyIsPressed;
        }

        private void registerSfstMenu()
        {
            // Here we are registering the command with the client. It does not need to be on tick as after it registers 1 time, it will remain on.
            API.RegisterCommand("sfst", new Action(() => {

                // Here we are going to check if the player is on duty
                if (!Utilities.IsPlayerOnDuty())
                {
                    Screen.ShowNotification("You are currently not on duty.");
                    return;
                }

                // Here we are going to check if the player is on foot
                if (Game.PlayerPed.IsInVehicle())
                {
                    Screen.ShowNotification("You must be on foot.");
                    return;
                }

                // With this we are activating or deactivating the menu
                sfstMenu.Visible = !sfstMenu.Visible;

            }), false);

            // This registers they key to be customizable on FiveM directly.
            API.RegisterKeyMapping("sfst", "Standardized Field Sobriety Testing Menu", "keyboard", "F3");
        }

        // This is is what will happen on every tick
        /*
        public async Task checkIfMenuKeyIsPressed()
        {         
            // Now we are checking if the player is pressing the proper key to activate the menu
            // This piece of code is to execute it hard coded on F3
            
            if (Game.IsControlJustPressed(0, Control.SaveReplayClip)) // Check if a specific key is pressed (use F5 as an example)
            {
                // Here we are going to check 
                if (!Utilities.IsPlayerOnDuty())
                {
                    Screen.ShowNotification("You are currently not on duty.");
                    return;
                }

                if (Game.PlayerPed.IsInVehicle())
                {
                    Screen.ShowNotification("You must be on foot.");
                    return;
                }
                // With this we are activating or deactivating the menu
                sfstMenu.Visible = !sfstMenu.Visible;
            }
            
        }
        */

        public void AddSfstOptions() // Method to add options to the menu
        {
            // First we set the menu to show on the left
            MenuController.MenuAlignment = MenuController.MenuAlignmentOption.Left;

            // Now we create the actual menu title and items
            sfstMenu = new Menu("SFST Menu", "Please select an option");

            // Now we add the menu to the controller
            MenuController.AddMenu(sfstMenu);

            // Now we start creating the items of the menu
            MenuItem nystagmusTest = new MenuItem("Horizontal Gaze Nystagmus Test");
            MenuItem oneLegStandTest = new MenuItem("One Leg Stand Test", "Perform the one leg stand test");
            MenuItem walkAndTurnTest = new MenuItem("Walk and Turn Test");
            MenuItem clearAnimations = new MenuItem("Stop test", "Instruct the suspect to stop");

            // And now we add the items to the menu
            sfstMenu.AddMenuItem(nystagmusTest);
            sfstMenu.AddMenuItem(oneLegStandTest);
            sfstMenu.AddMenuItem(walkAndTurnTest);
            sfstMenu.AddMenuItem(clearAnimations);

            sfstMenu.OnItemSelect += (_menu, _item, _index) =>
            {
                if (_index == nystagmusTest.Index) { startTest(TEST_OPTIONS.NYSTAGMUS); } 
                else if (_index == oneLegStandTest.Index) { startTest(TEST_OPTIONS.ONE_LEG_STAND);  }
                else if (_index == walkAndTurnTest.Index) { startTest(TEST_OPTIONS.WALK_AND_TURN); }
                else if (_index == clearAnimations.Index) { startTest(TEST_OPTIONS.CLEAR); }
            };
        }

        public async Task startTest(int testType)
        {
            if (!Utilities.IsPlayerPerformingTrafficStop())
            {
                Screen.ShowNotification("You are currently not in a traffic stop.");
                return;
            }

            // Now we are setting the variables to identify the ped
            tsDriver = Utilities.GetDriverFromTrafficStop();
            tsVehicle = Utilities.GetVehicleFromTrafficStop();
            player = Game.PlayerPed;

            // And setting new variables as well to gather ped data
            PedData pedInformation = await tsDriver.GetData();
            VehicleData vehicleInformation = await tsVehicle.GetData();
            double BAC = pedInformation.BloodAlcoholLevel;
            bool isDrugged = checkIfDrugged(pedInformation.UsedDrugs);
            int drunkLevel = getDrunkLevel(pedInformation.BloodAlcoholLevel);

            // Now we get the drunk animation at random based on the test and level of intoxication
            string[] drunkAnimation;
            string[] fallAnimation;
            TaskSequence animSequence;
            TaskSequence animSequencePlayer;

            // And we get a milisecond random number to include a fall
            int animationDurationBeforeFall = RandomUtils.GetRandomNumber(1000, 10001);
            int fallOdds = RandomUtils.GetRandomNumber(1, 101);

            if (tsDriver.IsSittingInVehicle())
            {
                Screen.ShowNotification("The suspect is currently inside the vehicle.");
                return;
            }

            if (testType == TEST_OPTIONS.NYSTAGMUS)
            {
                if(drunkLevel == DRUNK_LEVEL.SOBER)
                {
                    tsDriver.Task.PlayAnimation("amb@lo_res_idles@", "world_human_yoga_male_lo_res_base", 8f, -1, AnimationFlags.Loop);

                    // Now the player part
                    animSequencePlayer = new TaskSequence();
                    animSequencePlayer.AddTask.PlayAnimation("oddjobs@suicide", "bystander_pointhigh", 8f, 5000, AnimationFlags.None);
                    animSequencePlayer.AddTask.PlayAnimation("oddjobs@suicide", "bystander_pointoutof", 8f, -1, AnimationFlags.None);
                    player.Task.PerformSequence(animSequencePlayer);

                } 
                else if (drunkLevel == DRUNK_LEVEL.SLIGHTLY_DRUNK)
                {
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    tsDriver.Task.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, -1, AnimationFlags.Loop);

                    // Now the player part
                    animSequencePlayer = new TaskSequence();
                    animSequencePlayer.AddTask.PlayAnimation("oddjobs@suicide", "bystander_pointhigh", 8f, 5000, AnimationFlags.None);
                    animSequencePlayer.AddTask.PlayAnimation("oddjobs@suicide", "bystander_pointoutof", 8f, -1, AnimationFlags.None);
                    player.Task.PerformSequence(animSequencePlayer);
                }
                else if (drunkLevel == DRUNK_LEVEL.DRUNK)
                {
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    animSequence = new TaskSequence();
                    animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.Loop);
                    animSequence.AddTask.PlayAnimation("move_fall@beastjump", "high_land_stand", 8f, -1, AnimationFlags.None);
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.Loop);
                    tsDriver.Task.PerformSequence(animSequence);

                    // Now the player part
                    animSequencePlayer = new TaskSequence();
                    animSequencePlayer.AddTask.PlayAnimation("oddjobs@suicide", "bystander_pointhigh", 8f, 5000, AnimationFlags.None);
                    animSequencePlayer.AddTask.PlayAnimation("oddjobs@suicide", "bystander_pointoutof", 8f, -1, AnimationFlags.None);
                    player.Task.PerformSequence(animSequencePlayer);

                }
                else if (drunkLevel == DRUNK_LEVEL.VERY_DRUNK)
                {
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    animSequence = new TaskSequence();
                    animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.Loop);
                    animSequence.AddTask.PlayAnimation("move_fall@beastjump", "low_land_stand", 8f, -1, AnimationFlags.None);
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.Loop);
                    animSequence.AddTask.PlayAnimation("move_fall@beastjump", "low_land_stand", 8f, -1, AnimationFlags.None);
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.Loop);
                    tsDriver.Task.PerformSequence(animSequence);

                    // Now the player part
                    animSequencePlayer = new TaskSequence();
                    animSequencePlayer.AddTask.PlayAnimation("oddjobs@suicide", "bystander_pointhigh", 8f, 5000, AnimationFlags.None);
                    animSequencePlayer.AddTask.PlayAnimation("oddjobs@suicide", "bystander_pointoutof", 8f, -1, AnimationFlags.None);
                    player.Task.PerformSequence(animSequencePlayer);
                }

            }

            if (testType == TEST_OPTIONS.ONE_LEG_STAND)
            {
                if(drunkLevel == DRUNK_LEVEL.SOBER)
                {                  
                    tsDriver.Task.PlayAnimation("rcmjosh2", "stand_lean_back_beckon_a", 8f, -1, AnimationFlags.None);
                }             
                else if(drunkLevel == DRUNK_LEVEL.SLIGHTLY_DRUNK)
                {
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    animSequence = new TaskSequence();

                    if (fallOdds <= 25)
                    {
                        animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.Loop);
                        animSequence.AddTask.PlayAnimation("veh@std@ds@enter_exit", "jack_to_stand", 8f, -1, AnimationFlags.None);
                    }
                    else
                    {
                        animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, -1, AnimationFlags.None);
                    }
                        

                    tsDriver.Task.PerformSequence(animSequence);
                }
                else if(drunkLevel == DRUNK_LEVEL.DRUNK)
                {
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    fallAnimation = getRandomFallAnimation(testType, drunkLevel).Split(',');
                    animSequence = new TaskSequence();

                    if(fallOdds <= 50)
                    {
                        animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.Loop);
                        animSequence.AddTask.PlayAnimation(fallAnimation[0], fallAnimation[1], 8f, -1, AnimationFlags.None);
                    } 
                    else
                    {
                        animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, -1, AnimationFlags.Loop);
                    }
                    
                    tsDriver.Task.PerformSequence(animSequence);
                }
                else if (drunkLevel == DRUNK_LEVEL.VERY_DRUNK)
                {
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    fallAnimation = getRandomFallAnimation(testType, drunkLevel).Split(',');
                    animSequence = new TaskSequence();

                    if(fallOdds <= 75)
                    {
                        animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.Loop);
                        animSequence.AddTask.PlayAnimation(fallAnimation[0], fallAnimation[1], 8f, -1, AnimationFlags.None);
                    }
                    else
                    {
                        animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, -1, AnimationFlags.Loop);
                    }
                    
                    tsDriver.Task.PerformSequence(animSequence);
                }
            }

            if (testType == TEST_OPTIONS.WALK_AND_TURN)
            {
                if(drunkLevel == DRUNK_LEVEL.SOBER)
                {
                    animSequence = new TaskSequence();
                    animSequence.AddTask.PlayAnimation("anim@move_m@security_guard", "walk", 8f, 6000, AnimationFlags.None);
                    animSequence.AddTask.PlayAnimation("get_up@directional@movement@walk_starts", "get_up_-180_l", 8f, 2000, AnimationFlags.None);
                    animSequence.AddTask.PlayAnimation("anim@move_m@security_guard", "walk", 8f, 6000, AnimationFlags.None);
                    tsDriver.Task.PerformSequence(animSequence);
                }
                else if(drunkLevel == DRUNK_LEVEL.SLIGHTLY_DRUNK)
                {
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    animSequence = new TaskSequence();
                    animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, 6000, AnimationFlags.None);
                    animSequence.AddTask.PlayAnimation("anim@move_m@grooving@slow@", "walk_turn_l3", 8f, 2000, AnimationFlags.None);
                    animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, 6000, AnimationFlags.None);
                    tsDriver.Task.PerformSequence(animSequence);
                }
                else if(drunkLevel == DRUNK_LEVEL.DRUNK)
                {
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    fallAnimation = getRandomFallAnimation(testType, drunkLevel).Split(',');
                    animSequence = new TaskSequence();

                    if (fallOdds <= 50)
                    {
                        if (fallOdds <= 25)
                        {
                            animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.None);
                            animSequence.AddTask.PlayAnimation(fallAnimation[0], fallAnimation[1], 8f, -1, AnimationFlags.None);
                        }
                        else
                        {
                            animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.None);
                            animSequence.AddTask.PlayAnimation("move_fall@beastjump", "high_land_stand", 8f, -1, AnimationFlags.None);
                            animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.None);
                            animSequence.AddTask.PlayAnimation("move_m@drunk@moderatedrunk", "idle_turn_l_180", 8f, 2000, AnimationFlags.None);
                            animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.None);
                            animSequence.AddTask.PlayAnimation("move_fall@beastjump", "high_land_stand", 8f, -1, AnimationFlags.None);
                            animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, 2000, AnimationFlags.None);
                        }
                    }
                    else
                    {
                        animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, 6000, AnimationFlags.None);
                        animSequence.AddTask.PlayAnimation("move_m@drunk@moderatedrunk", "idle_turn_l_180", 8f, 2000, AnimationFlags.None);
                        animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, 6000, AnimationFlags.None);
                    }

                    tsDriver.Task.PerformSequence(animSequence);
                }
                else if(drunkLevel == DRUNK_LEVEL.VERY_DRUNK)
                {
                    drunkAnimation = getRandomDrunkAnimation(testType, drunkLevel).Split(',');
                    fallAnimation = getRandomFallAnimation(testType, drunkLevel).Split(',');
                    animSequence = new TaskSequence();

                    if (fallOdds <= 75)
                    {
                        animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, animationDurationBeforeFall, AnimationFlags.None);
                        animSequence.AddTask.PlayAnimation(fallAnimation[0], fallAnimation[1], 8f, -1, AnimationFlags.None);
                    }
                    else
                    {
                        animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, 6000, AnimationFlags.None);
                        animSequence.AddTask.PlayAnimation("move_m@drunk@moderatedrunk", "idle_turn_l_180", 8f, 2000, AnimationFlags.None);
                        animSequence.AddTask.PlayAnimation(drunkAnimation[0], drunkAnimation[1], 8f, 6000, AnimationFlags.None);
                    }

                    tsDriver.Task.PerformSequence(animSequence);
                }

            }

            if (testType == TEST_OPTIONS.CLEAR)
            {
                tsDriver.Task.PlayAnimation("move_m@casual@a", "idle", 8f, 1000, AnimationFlags.None);
                player.Task.PlayAnimation("move_m@casual@a", "idle", 8f, 1000, AnimationFlags.None);
            }
        }

        private bool checkIfDrugged(PedData.Drugs[] usedDrugs)
        {
            if (usedDrugs.Length >= 1)
            {
                if (usedDrugs[0] == PedData.Drugs.Meth || usedDrugs[0] == PedData.Drugs.Cocaine || usedDrugs[0] == PedData.Drugs.Marijuana)
                {
                    return true;
                }
            }

            if (usedDrugs.Length >= 2)
            {
                if (usedDrugs[1] == PedData.Drugs.Meth || usedDrugs[1] == PedData.Drugs.Cocaine || usedDrugs[1] == PedData.Drugs.Marijuana)
                {
                    return true;
                }
            }

            if (usedDrugs.Length >= 3)
            {
                if (usedDrugs[2] == PedData.Drugs.Meth || usedDrugs[2] == PedData.Drugs.Cocaine || usedDrugs[2] == PedData.Drugs.Marijuana)
                {
                    return true;
                }
            }

            return false;
        }

        private int getDrunkLevel(double bac)
        {
            if (bac >= 0.01 && bac <= 0.07) return DRUNK_LEVEL.SLIGHTLY_DRUNK;
            else if (bac >= 0.08 && bac <= 0.1) return DRUNK_LEVEL.DRUNK;
            else if (bac >= 0.11) return DRUNK_LEVEL.VERY_DRUNK;

            return DRUNK_LEVEL.SOBER;
        }

        private string getRandomFallAnimation(int testType, int drunkLevel)
        {
            List<string> fallAnimation = null;

            if(drunkLevel == DRUNK_LEVEL.SLIGHTLY_DRUNK)
            {
                fallAnimation = new List<string>()
                {
                "missfam5_yoga,c3_fail_to_start",
                "random@drunk_driver_1,drunk_fall_over",
                };
            }
            else if(drunkLevel == DRUNK_LEVEL.DRUNK)
            {
                fallAnimation = new List<string>()
                {
                "missfam5_yoga,c3_fail_to_start",
                "missmic2@goon1,goonfall_into_grinder",
                "move_fall,land_fall",
                "veh@van@side_fps,dead_fall_out",
                };
            }
            else if(drunkLevel == DRUNK_LEVEL.VERY_DRUNK)
            {
                fallAnimation = new List<string>()
                {
                "anim@amb@nightclub@mini@drinking@drinking_shots@ped_c@drunk,outro_fallover",
                "anim@amb@nightclub@mini@drinking@drinking_shots@ped_b@drunk,outro_fallover",
                "anim@amb@nightclub@mini@drinking@drinking_shots@ped_d@drunk,outro_fallover",
                "random@drunk_driver_1,drunk_fall_over",
                "missarmenian2,punch_reaction_&_fall_drunk",
                };
            }

            return fallAnimation.SelectRandom();
        }

        private string getRandomDrunkAnimation(int testType, int drunkLevel)
        {

            List<string> drunkAnimation = null;

            if (testType == TEST_OPTIONS.NYSTAGMUS)
            {
                if(drunkLevel == DRUNK_LEVEL.SLIGHTLY_DRUNK)
                {
                    drunkAnimation = new List<string>()
                    {
                    "amb@world_human_yoga@male@base,base",
                    "missfbi3_party_d,stand_talk_loop_a_male2",
                    "missfbi3_party_d,stand_talk_loop_b_male2",
                    "missfbi3_party_d,walk_from_b_to_a_male1",
                    "move_m@drunk@slightlydrunk,idle",
                    };
                }
                else if(drunkLevel == DRUNK_LEVEL.DRUNK)
                {
                    drunkAnimation = new List<string>()
                    {
                    "missfam5_yoga,a1_pose",
                    "missfam5_yoga,c8_pose",
                    "missfbi3_party_d,stand_talk_loop_a_female",
                    "missfbi3_party_d,stand_talk_loop_b_male3",
                    "missfbi3_party_d,walk_from_a_to_b_female",
                    "missfbi3_party_d,walk_from_a_to_b_male1",
                    "missfbi3_party_d walk_from_a_to_b_male2",
                    "amb@world_human_bum_standing@drunk@base,base",
                    "move_m@drunk@verydrunk_idles@,fidget_06",
                    "move_m@drunk@verydrunk_idles@,fidget_07",
                    "move_m@drunk@verydrunk_idles@,fidget_08",
                    "move_m@drunk@verydrunk_idles@,fidget_09",
                    "move_m@drunk@verydrunk,idle",
                    "move_m@drunk@moderatedrunk_head_up,idle",
                    "move_m@drunk@moderatedrunk_head_up,idle_turn_l_0",
                    "move_m@drunk@moderatedrunk_idles@,flinch_b",
                    };
                }
                else if(drunkLevel == DRUNK_LEVEL.VERY_DRUNK)
                {
                    drunkAnimation = new List<string>()
                    {
                    "missarmenian2,standing_idle_loop_drunk",
                    "missfbi3_party_d,walk_from_a_to_b_female",
                    "missfbi3_party_d,walk_from_b_to_a_female",
                    "amb@world_human_bum_standing@drunk@idle_a,idle_a",
                    "amb@world_human_bum_standing@drunk@idle_a,idle_c",
                    "amb@world_human_bum_standing@drunk@idle_a,idle_b",
                    "move_m@drunk@verydrunk_idles@,fidget_01",
                    "move_m@drunk@verydrunk_idles@,fidget_02",
                    "move_m@drunk@verydrunk_idles@,fidget_03",
                    "move_m@drunk@verydrunk_idles@,fidget_04",
                    "move_m@drunk@verydrunk_idles@,fidget_05",
                    "move_m@drunk@moderatedrunk,idle",
                    };
                }
            }//////////////////////////////////////////////////////////////////////////////
            else if(testType == TEST_OPTIONS.ONE_LEG_STAND)
            {
                if (drunkLevel == DRUNK_LEVEL.SLIGHTLY_DRUNK)
                {
                    drunkAnimation = new List<string>()
                    {
                    "move_climb,clamberpose_stand_angled_20",
                    "amb@world_human_yoga@female@base,base_c",
                    "missfam5_yoga,c8_pose",
                    "missheistdocks2bleadinoutlsdh_2b_int,leg_massage_b_trevor",
                    };
                }
                else if (drunkLevel == DRUNK_LEVEL.DRUNK)
                {
                    drunkAnimation = new List<string>()
                    {                 
                    "missheistdocks2bleadinoutlsdh_2b_int,leg_massage_trevor ",
                    "amb@world_human_leaning@male@wall@back@foot_up@react_shock,front",
                    "switch@trevor@drunk_howling_sc,loop",
                    "missfbi3_party_d,stand_talk_loop_a_male2",
                    "move_m@drunk@moderatedrunk_head_up,idle",
                    "move_m@drunk@moderatedrunk_head_up,idle_turn_l_0",
                    };
                }
                else if (drunkLevel == DRUNK_LEVEL.VERY_DRUNK)
                {
                    drunkAnimation = new List<string>()
                    {
                    "missarmenian2,standing_idle_loop_drunk",
                    "missarmenian3leadinoutarmenian_3_int,_leadin_look_right_simeon",
                    "random@drunk_driver_1,drunk_argument_dd2",
                    "amb@world_human_bum_standing@drunk@base,base",
                    "amb@world_human_bum_standing@drunk@idle_a,idle_b",
                    };
                }
            }//////////////////////////////////////////////////////////////////////////////
            else if(testType == TEST_OPTIONS.WALK_AND_TURN)
            {
                if (drunkLevel == DRUNK_LEVEL.SLIGHTLY_DRUNK)
                {
                    drunkAnimation = new List<string>()
                    {
                    "amb@world_human_power_walker@female@base,base",
                    "anim@move_f@grooving@slow@,walk",
                    "move_m@favor_right_foot,walk",
                    };
                }
                else if (drunkLevel == DRUNK_LEVEL.DRUNK)
                {
                    drunkAnimation = new List<string>()
                    {
                    "move_m@favor_right_foot,walk",
                    "move_m@drunk@a,walk",
                    "move_drunk_m,walk",
                    "move_f@drunk@a,walk",
                    };
                }
                else if (drunkLevel == DRUNK_LEVEL.VERY_DRUNK)
                {
                    drunkAnimation = new List<string>()
                    {
                    "move_m@drunk@a,walk",
                    "move_drunk_m,walk",
                    "move_f@drunk@a,walk",
                    "move_m@drunk@verydrunk",
                    "move_m@drunk@moderatedrunk,walk_turn_r4",
                    "move_strafe@first_person@drunk,idle_turn_right_fast",
                    "abigail_mcs_1_concat-1,player_zero_dual-1",
                    };
                }
            }

            return drunkAnimation.SelectRandom();
        }

        private string getRandomFallAnimation()
        {
            List<string> fallAnimation = new List<string>()
            {
            "move_fall@beastjump,high_land_stand",
            "move_fall@beastjump,low_land_stand",
            };

            return fallAnimation.SelectRandom();
        }
    }
}
