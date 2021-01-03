using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace ZootrDiscordBot
{
    class Program
    {
        // Setup client and global vars
        private static DiscordSocketClient discordclient;
        private static string TokenKey = "";
        public static string Channel = "";
        public static string SpoilChannel = "";
        public static string OoTDirectory = "";

        // Store the default season 4 settions to compare against
        static readonly JObject DefaultSettings = JObject.Parse(@"{
            'world_count':                             1,
            'create_spoiler':                          true,
            'randomize_settings':                      false,
            'open_forest':                             'closed_deku',
            'open_kakariko':                           'open',
            'open_door_of_time':                       true,
            'zora_fountain':                           'closed',
            'gerudo_fortress':                         'fast',
            'bridge':                                  'medallions',
            'bridge_medallions':                       2,
            'triforce_hunt':                           false,
            'logic_rules':                             'glitchless',
            'all_reachable':                           true,
            'bombchus_in_logic':                       false,
            'one_item_per_dungeon':                    false,
            'trials_random':                           false,
            'trials':                                  0,
            'skip_child_zelda':                        true,
            'no_escape_sequence':                      true,
            'no_guard_stealth':                        true,
            'no_epona_race':                           true,
            'skip_some_minigame_phases':               true,
            'useful_cutscenes':                        false,
            'complete_mask_quest':                     false,
            'fast_chests':                             true,
            'logic_no_night_tokens_without_suns_song': false,
            'free_scarecrow':                          false,
            'fast_bunny_hood':                         true,
            'start_with_rupees':                       false,
            'start_with_consumables':                  true,
            'starting_hearts':                         3,
            'chicken_count_random':                    false,
            'chicken_count':                           7,
            'big_poe_count_random':                    false,
            'big_poe_count':                           1,
            'shuffle_kokiri_sword':                    true,
            'shuffle_ocarinas':                        false,
            'shuffle_gerudo_card':                     false,
            'shuffle_song_items':                      'song',
            'shuffle_cows':                            false,
            'shuffle_beans':                           false,
            'shuffle_medigoron_carpet_salesman':       false,
            'shuffle_interior_entrances':              'off',
            'shuffle_grotto_entrances':                false,
            'shuffle_dungeon_entrances':               false,
            'shuffle_overworld_entrances':             false,
            'owl_drops':                               false,
            'warp_songs':                              false,
            'spawn_positions':                         true,
            'shuffle_scrubs':                          'off',
            'shopsanity':                              'off',
            'tokensanity':                             'off',
            'shuffle_mapcompass':                      'startwith',
            'shuffle_smallkeys':                       'dungeon',
            'shuffle_fortresskeys':                    'vanilla',
            'shuffle_bosskeys':                        'dungeon',
            'shuffle_ganon_bosskey':                   'lacs_medallions',
            'lacs_medallions':                         6,
            'enhance_map_compass':                     false,
            'mq_dungeons_random':                      false,
            'mq_dungeons':                             0,
            'disabled_locations':                      [
              'Deku Theater Mask of Truth'
            ],
            'allowed_tricks':                          [
              'logic_fewer_tunic_requirements',
              'logic_grottos_without_agony',
              'logic_child_deadhand',
              'logic_man_on_roof',
              'logic_dc_jump',
              'logic_rusted_switches',
              'logic_windmill_poh',
              'logic_crater_bean_poh_with_hovers',
              'logic_forest_vines',
              'logic_lens_botw',
              'logic_lens_castle',
              'logic_lens_gtg',
              'logic_lens_shadow',
              'logic_lens_shadow_back',
              'logic_lens_spirit'
            ],
            'logic_earliest_adult_trade':              'prescription',
            'logic_latest_adult_trade':                'claim_check',
            'starting_equipment':                      [
              'deku_shield'
            ],
            'starting_items':                          [
              'ocarina'
            ],
            'starting_songs':                          [],
            'ocarina_songs':                           false,
            'correct_chest_sizes':                     false,
            'clearer_hints':                           true,
            'no_collectible_hearts':                   false,
            'hints':                                   'always',
            'hint_dist':                               'tournament',
            'item_hints':                              [],
            'hint_dist_user':                          {},
            'text_shuffle':                            'none',
            'ice_trap_appearance':                     'junk_only',
            'junk_ice_traps':                          'off',
            'item_pool_value':                         'balanced',
            'damage_multiplier':                       'normal',
            'starting_tod':                            'default',
            'starting_age':                            'random'
        }");

        // Start discord
        static async Task Main(string[] args)
        {
            // Load all our config files
            TokenKey = System.IO.File.ReadAllText("Token.conf").Trim();
            OoTDirectory = System.IO.File.ReadAllText("OOTDir.conf").Trim();
            Channel = System.IO.File.ReadAllText("Channel.conf").Trim();
            SpoilChannel = System.IO.File.ReadAllText("SpoilerChannel.conf").Trim();
            // Used for watching reactions
            var config = new DiscordSocketConfig { MessageCacheSize = 100 };
            discordclient = new DiscordSocketClient(config);
            discordclient.Log += LogAsync;
            discordclient.Ready += Ready;
            discordclient.MessageReceived += MessageReceivedAsync;
            // Start the bot
            await discordclient.LoginAsync(TokenType.Bot, TokenKey);
            await discordclient.StartAsync();
            await discordclient.SetGameAsync("ZOOTR", null, ActivityType.Playing);
            // Create timer for adding and removing the racing role
            System.Timers.Timer timer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds)
            {
                AutoReset = true
            };
            timer.Elapsed += new System.Timers.ElapsedEventHandler(TimerEvent);
            timer.Start();
            await Task.Delay(-1);
        }
        public static async void TimerEvent(object sender, ElapsedEventArgs e)
        {
            // Check all guilds we are in
            foreach (var guild in discordclient.Guilds)
            {
                // Make sure we have the racing role
                if (!guild.Roles.Where(x => x.Name.ToLower() == "racing").Any())
                {
                    try
                    {
                        // If the racing role does not exist, make it and the channels needed
                        await guild.CreateRoleAsync("racing", null, null, false, null);
                        await guild.CreateTextChannelAsync(Channel);
                        await guild.CreateTextChannelAsync(SpoilChannel);
                        var chan = guild.Channels.FirstOrDefault(x => x.Name.ToLower() == SpoilChannel.ToLower());
                        var roleInternal = guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "racing");
                        await chan.AddPermissionOverwriteAsync(roleInternal, OverwritePermissions.DenyAll(chan));
                    }
                    catch { }
                }

                // Locate the role
                var role = guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "racing");
                List<SocketGuildUser> UsersInVoice = new List<SocketGuildUser>();
                // Find all users in voice channels
                foreach (var chan in guild.VoiceChannels)
                {
                    try
                    {
                        foreach (var user in chan.Users)
                        {
                            UsersInVoice.Add(user);
                        }
                    }
                    catch { }
                }

                // Add the role or remove the role if they are in a voice channel
                foreach (var usr in guild.Users)
                {
                    if (UsersInVoice.Contains(usr))
                    {
                        if (!usr.Roles.Contains(role))
                        {
                            try
                            {
                                await usr.AddRoleAsync(role);
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        if (usr.Roles.Contains(role))
                        {
                            try
                            {
                                await usr.RemoveRoleAsync(role);
                            }
                            catch { }
                        }
                    }

                }
            }
        }
        public static Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
        public static Task Ready()
        {
            Console.WriteLine(discordclient.CurrentUser + " is connected!");
            return Task.CompletedTask;
        }
        public static async Task MessageReceivedAsync(SocketMessage message)
        {
            // The bot should never respond to itself.
            if (message.Author.Id == discordclient.CurrentUser.Id)
                return;
            try
            {
                if (message.Content != string.Empty)
                {
                    // Verify its a settings string length and we messaged the bot
                    if (message.Content.Length > 65)
                    {
                        if (message.MentionedUsers.Where(x => x.Id == discordclient.CurrentUser.Id).Any())
                        {
                            _ = Task.Run(() => MessagePassed(message));
                        }
                    }
                }
            }
            catch { }
        }

        public static async void MessagePassed(SocketMessage message)
        {
            // Validate that this is a valid settings string
            var resp = RunPython("--output_settings  --convert_settings --settings_string " + message.Content.Replace("<@!782757480887091260>", "").Trim());
            if (resp.Item1)
            {
                // Parse the spoiler log
                JObject Settings = JObject.Parse(resp.Item2);
                // Update some defaults
                Settings["compress_rom"] = "Patch";
                Settings["create_spoiler"] = true;
                Settings["create_cosmetics_log"] = false;
                // Create a temp settings file
                string fileName = Path.GetDirectoryName(OoTDirectory) + "/" + Guid.NewGuid().ToString();
                System.IO.File.WriteAllText(fileName, Settings.ToString());
                // Generate a seed using our settings file
                var GeneratedSeedResponse = RunPython("--settings " + fileName);
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
                // Find the channels set by the config files
                ISocketMessageChannel FoundChannel = null;
                ISocketMessageChannel SpoilerChannel = null;
                var chan = message.Channel as SocketGuildChannel;
                foreach (var gchan in chan.Guild.Channels)
                {
                    if (gchan.Name.ToLower() == Channel.ToLower())
                    {
                        FoundChannel = gchan as ISocketMessageChannel;
                    }
                    if (gchan.Name.ToLower() == SpoilChannel.ToLower())
                    {
                        SpoilerChannel = gchan as ISocketMessageChannel;
                    }
                }

                if (FoundChannel != null)
                {
                    // Parse the spoiler log for related settings
                    var SpoilerInfo = GetSpoilerInfo(System.IO.File.ReadAllText(Path.GetDirectoryName(GeneratedSeedResponse.Item4) + @"/" + GeneratedSeedResponse.Item3));

                    string BuiltMessage = "";
                    // Create the message to send
                    BuiltMessage += "**Seed Posted by**: `" + message.Author + "`\n";
                    if (SpoilerInfo.Item2 != null)
                    {
                        BuiltMessage += "**Settings String**: `" + SpoilerInfo.Item4 + "`\n";
                        BuiltMessage += "**Version**: `" + SpoilerInfo.Item5 + "`\n";
                        BuiltMessage += "**Seed Settings**: ```" + SpoilerInfo.Item1 + "```";
                    }
                    BuiltMessage += "\nWas this a good seed? Vote using thumbs up or down. You can abstain from voting.";
                    BuiltMessage += "\nYou can patch your OoT file at: https://ootrandomizer.com/generator";
                    var sentmessage = await FoundChannel.SendMessageAsync(BuiltMessage);
                    // Send the rest of the data
                    await FoundChannel.SendFileAsync(GeneratedSeedResponse.Item4);
                    await sentmessage.AddReactionsAsync(new[] { new Emoji("👍"), new Emoji("👎") });
                    await message.Channel.SendMessageAsync("Seed info has been posted to #seed-info");
                    // Read in the spoiler log into memory so we don't store it on disk
                    var SpoilerText = File.ReadAllText(Path.GetDirectoryName(GeneratedSeedResponse.Item4) + @"/" + GeneratedSeedResponse.Item3);
                    _ = Task.Run(() => SendSpoilerLog(SpoilerText, GeneratedSeedResponse.Item3, SpoilerChannel));
                    // Cleanup the files
                    if (File.Exists(Path.GetDirectoryName(GeneratedSeedResponse.Item4) + @"/" + GeneratedSeedResponse.Item3))
                    {
                        File.Delete(Path.GetDirectoryName(GeneratedSeedResponse.Item4) + @"/" + GeneratedSeedResponse.Item3);
                    }
                    if (File.Exists(GeneratedSeedResponse.Item4))
                    {
                        File.Delete(GeneratedSeedResponse.Item4);
                    }
                }
                else
                {
                    await message.Channel.SendMessageAsync("seed-info Channel does not exist.");
                }
            }
            else
            {
                await message.Channel.SendMessageAsync("That is not a valid settings string.");

            }


        }

        public static async void SendSpoilerLog(string SpoilerLog, string LogName, ISocketMessageChannel FoundChannel)
        {
            // Wait 20 minutes then write the file to a temp file to send it off
            System.Threading.Thread.Sleep(TimeSpan.FromMinutes(20));
            string fileName = LogName;
            System.IO.File.WriteAllText(fileName, SpoilerLog);
            await FoundChannel.SendFileAsync(fileName);
            // Cleanup
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }


        static (string, JObject, string, string, string) GetSpoilerInfo(string SettingString)
        {
            try
            {
                // Parse the found spoiler logs settings
                JObject SpoilerLog = JObject.Parse(SettingString);
                var Settings = SpoilerLog.SelectToken("settings");
                List<string> ModifiedSettings = new List<string>();
                foreach (var x in DefaultSettings)
                {
                    string name = x.Key;
                    JToken value = x.Value;
                    try
                    {
                        // For all the settings compare it against the existing list
                        string val1 = Settings.SelectToken(name).ToString();
                        string val2 = value.ToString();
                        if (val1 != val2)
                        {
                            ModifiedSettings.Add(name + ": " + Settings.SelectToken(name).ToString());
                        }
                    }
                    catch { }
                }
                // Create some specific vars to return for printouts
                string seedstr = SpoilerLog.SelectToken(":seed").ToString();
                string settingsstr = SpoilerLog.SelectToken(":settings_string").ToString();
                string versionstr = SpoilerLog.SelectToken(":version").ToString();
                // Return the data if its not default
                if (ModifiedSettings.Count() > 0)
                {
                    return (string.Join("\n", ModifiedSettings), SpoilerLog, seedstr, settingsstr, versionstr);
                }
                else
                {
                    return ("Default S4 Settings", SpoilerLog, seedstr, settingsstr, versionstr);
                }
            }
            catch
            {
                return ("", null, "", "", "");
            }

        }

        private static Tuple<bool, string, string, string> RunPython(string args)
        {
            // Start python using the params passed
            Process process = new Process();
            process.StartInfo.FileName = "python3"; 
            process.StartInfo.Arguments = string.Format("{0} {1}", OoTDirectory, args);
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            process.WaitForExit();
            // Read the data returned from the cli
            string result = process.StandardOutput.ReadToEnd();
            result += process.StandardError.ReadToEnd();
            // If we did not fail return the data cleaned up
            if (process.ExitCode == 0)
            {
                string spoiler = "";
                string patch = "";
                // Split the lines because of how the console redirect works
                foreach (var line in result.Split(new[] { '\r', '\n' }))
                {
                    if (line.Contains("Created spoiler log at"))
                    {
                        spoiler = line.Replace("Created spoiler log at: ", "");
                    }
                    if (line.Contains("Created patchfile at"))
                    {
                        patch = line.Replace("Created patchfile at: ", "");
                    }
                }
                return new Tuple<bool, string, string, string>(true, result, spoiler, patch);
            }
            else
            {
                return new Tuple<bool, string, string, string>(false, "", "", "");
            }

        }


    }
}
