using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;

namespace ZeroPlus.Oms.Ui.Notifications
{
    public class SoundManager
    {
        private const string SOUNDS_DIRECTORY = "Sounds";
        private const string USER_SOUNDS_DIRECTORY = "ZeroPlus OMS Sounds";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly List<string> allowedExtentions = new() { ".wav" };
        private static readonly ConcurrentDictionary<string, SoundPlayer> soundNameToPlayerDictionary = new();

        public static List<string> LoadedSounds { get; private set; } = new List<string>();

        public static void InitializeSoundPlayers()
        {
            try
            {
                DirectoryInfo path = Directory.GetParent(SOUNDS_DIRECTORY);
                string[] fileEntries = Directory.GetFiles(SOUNDS_DIRECTORY);
                foreach (string filePath in fileEntries)
                {
                    string fullPath = Path.Combine(path.FullName, filePath);
                    ProcessFile(fullPath, false);
                }

                string userSoundsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), USER_SOUNDS_DIRECTORY);
                if (Directory.Exists(userSoundsDirectory))
                {
                    fileEntries = Directory.GetFiles(userSoundsDirectory);
                    foreach (string filePath in fileEntries)
                    {
                        ProcessFile(filePath, false);
                    }
                }
                else
                {
                    Directory.CreateDirectory(userSoundsDirectory);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(InitializeSoundPlayers)} -> Init SoundManager failed");
            }
        }

        public static void ProcessFile(string filePath, bool copy)
        {
            try
            {
                string ext = Path.GetExtension(filePath);

                if (allowedExtentions.Contains(ext.ToLower()))
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    bool added = AddSoundPlayerForFile(fileName, filePath);

                    if (added && copy)
                    {
                        File.Copy(filePath, $"{SOUNDS_DIRECTORY}\\{fileName}{ext}", true);
                    }
                }
                else
                {
                    _log.Error($"{ext} not allowed.");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ProcessFile));
            }
        }

        private static bool AddSoundPlayerForFile(string fileName, string filePath)
        {
            try
            {
                SoundPlayer soundPlayer = new(filePath);
                soundNameToPlayerDictionary.TryAdd(fileName.ToUpper(), soundPlayer);
                soundPlayer.LoadCompleted += SoundPlayer_LoadCompleted;
                soundPlayer.LoadAsync();
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddSoundPlayerForFile));
            }
            return false;
        }

        private static void SoundPlayer_LoadCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            SoundPlayer player = sender as SoundPlayer;
            string loadedSoundName = Path.GetFileNameWithoutExtension(player.SoundLocation);

            LoadedSounds.Add(loadedSoundName.ToUpper());
            _log.Info($"{nameof(SoundPlayer_LoadCompleted)} -> Player added for sound {player.SoundLocation}.");
        }

        public static void Play(string soundId)
        {
            if (!string.IsNullOrEmpty(soundId))
            {
                if (_log.IsTraceEnabled)
                {
                    _log.Trace($"{nameof(Play)} -> Play request for {soundId}");
                }

                soundId = soundId.ToUpper();

                bool playerFound = soundNameToPlayerDictionary.TryGetValue(soundId, out SoundPlayer soundPlayer);

                if (playerFound)
                {
                    try
                    {
                        if (OmsCore.Config.PlayNotificationSoundsInSequence)
                        {
                            soundPlayer.PlaySync();
                        }
                        else
                        {
                            soundPlayer.Play();
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(Play));
                    }
                }
            }
        }

        public static void Play(int id)
        {
            try
            {
                string name = soundNameToPlayerDictionary.Keys.OrderBy(x => x).ToArray()[id - 1];
                Play(name);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Play));
            }
        }

        internal static void StopAllSoundPlayers()
        {
            if (_log.IsTraceEnabled)
            {
                _log.Trace($"{nameof(StopAllSoundPlayers)} -> Stop all sounds request.");
            }

            foreach (SoundPlayer player in soundNameToPlayerDictionary.Values)
            {
                player.Stop();
            }
        }
    }
}
