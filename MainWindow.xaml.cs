using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Sus_Companion.Properties;

namespace Sus_Companion
{
    public partial class MainWindow : Window
    {
        // Enable sound by default
        private bool IsSoundEnabled = true;

        // Cache the loaded sound files
        private readonly Dictionary<string, MediaPlayer> _soundPlayers = [];

        // Whether the user is selecting a character
        private bool IsUserSelectingCharacter = false;

        // References to the user character image and label
        private Image? UserCharacterImage = null;
        private Label? UserCharacterLabel = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Window lifecycle methods

        /// <summary>
        /// Sets the window position based on saved settings.
        /// </summary>
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            // Preload sound files from embedded resources
            PreloadSounds();

            // Update the Stats label located at the bottom of the window
            UpdateStats();

            // Check if the window position is not default (0,0)
            if (Settings.Default.WindowTop != 0 || Settings.Default.WindowLeft != 0)
            {
                // Set the window position to the saved values
                Top = Settings.Default.WindowTop;
                Left = Settings.Default.WindowLeft;
            }

            // Enable or disable sound based on saved settings and set the icon accordingly
            IsSoundEnabled = Properties.Settings.Default.IsSoundEnabled;
            SoundPath.Data = Geometry.Parse(IsSoundEnabled
                ? "M3 11V13 M6 8V16 M9 10V14 M12 7V17 M15 4V20 M18 9V15 M21 11V13"
                : "M3 11V13 M6 11V13 M9 11V13 M12 10V14 M15 11V13 M18 11V13 M21 11V13");
            Sound_Button.Opacity = IsSoundEnabled ? 1.0 : 0.5;
        }

        /// <summary>
        /// Sets the window position before closing.
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.WindowTop = Top;
            Settings.Default.WindowLeft = Left;
            Settings.Default.Save();
            base.OnClosing(e);
        }

        /// <summary>
        /// Preloads MediaPlayer instances for each sound from embedded resources and keeps them ready for playback.
        /// </summary>
        private void PreloadSounds()
        {
            // List of sound file names to preload
            string[] soundNames = { "select.wav", "dead.wav", "refresh.wav", "sound-on.wav", "sound-off.wav", "alive.wav", "browser.wav", "close.wav" };

            foreach (string name in soundNames)
            {
                string resourcePath = $"Sus_Companion.assets.sounds.{name}";
                using Stream? stream = GetType().Assembly.GetManifestResourceStream(resourcePath);

                if (stream == null)
                {
                    _ = MessageBox.Show($"Sound resource {resourcePath} not found.");
                    continue;
                }

                // Load the embedded resource into a temporary WAV file
                string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");
                using (FileStream fs = File.Create(tempFile))
                {
                    stream.CopyTo(fs);
                }

                // Create and configure the MediaPlayer
                MediaPlayer player = new();
                player.Open(new Uri(tempFile, UriKind.Absolute));
                player.Stop();
                player.Volume = 0;

                _soundPlayers[name] = player;
            }
        }

        #endregion

        #region Character interaction methods

        /// <summary>
        /// Handles a left click on a character to cycle its state between SAFE, SUS, and ALIVE (or sets/unsets user if selecting).
        /// </summary>
        private void Character_LeftClick(object sender, MouseButtonEventArgs e)
        {
            // Check if the sender is an Image and if its Label exists
            if (sender is not Image img)
            {
                return;
            }

            if (FindName($"{img.Name}_Label") is not Label label)
            {
                return;
            }

            // If we're in "User Selection" mode
            if (IsUserSelectingCharacter)
            {
                // Prevent selecting a dead character
                if (img.Opacity < 1.0)
                {
                    return;
                }

                // Reset previous user selection (if any)
                if (UserCharacterLabel != null)
                {
                    UserCharacterLabel.Content = UserCharacterImage!.Name;
                    UserCharacterLabel.Foreground = Brushes.White;
                }

                // Assign the clicked character as the user
                UserCharacterImage = img;
                UserCharacterLabel = label;

                // Change the label to "YOU" and match its color
                label.Content = "YOU";
                label.Foreground = GetCharacterColor(img.Name);

                // Change the User image icon
                User.Source = img.Source;

                // Exit user selection mode
                IsUserSelectingCharacter = false;

                return;
            }


            // Prevent state cycling if this character is the user
            if (UserCharacterImage == img)
            {
                return;
            }

            // If the character is dead → set alive
            if (img.Opacity < 1.0)
            {
                img.Opacity = 1.0;
                img.Tag = 0;
                label.Foreground = Brushes.White;
                label.Content = img.Name;
                UpdateStats();
                return;
            }

            // Cycle state
            int state = img.Tag is int t ? t : 0;
            state = (state + 1) % 3;
            img.Tag = state;

            // Update label
            switch (state)
            {
                case 1:
                    label.Foreground = Brushes.LimeGreen;
                    label.Content = "SAFE";
                    PlaySound("select.wav", 0.2);
                    break;
                case 2:
                    label.Foreground = Brushes.Red;
                    label.Content = "SUS";
                    PlaySound("select.wav", 0.2);
                    break;
                default:
                    label.Foreground = Brushes.White;
                    label.Content = img.Name;
                    PlaySound("alive.wav", 0.2);
                    break;
            }

            UpdateStats();
        }


        /// <summary>
        /// Handles a right click on a character to toggle its DEAD/ALIVE (character's name) state.
        /// </summary>
        private void Character_RightClick(object sender, MouseButtonEventArgs e)
        {
            // Check if the sender is an Image and if its Label exists
            if (sender is not Image img)
            {
                return;
            }

            if (FindName($"{img.Name}_Label") is not Label label)
            {
                return;
            }

            // Prevent right click action if this character is the user
            if (UserCharacterImage == img)
            {
                // Optionally, you can show a tooltip or feedback here
                return;
            }

            // Check if the character is dead
            bool isDead = img.Opacity < 1.0;

            if (isDead)
            {
                // Set character to ALIVE state
                img.Opacity = 1.0;
                img.Tag = 0;
                label.Foreground = Brushes.White;
                label.Content = img.Name;
            }
            else
            {
                // Play the kill sound effect at 20% volume
                PlaySound("dead.wav", 0.2);

                // Set character to DEAD state
                img.Opacity = 0.15;
                label.Foreground = Brushes.DarkSlateGray;
                label.Content = "DEAD";
            }

            UpdateStats();
        }

        /// <summary>
        /// Handles a mouse click on the User button to enter user selection mode.
        /// </summary>
        private void User_Button_Click(object sender, MouseButtonEventArgs e)
        {
            // Play selection sound
            PlaySound("select.wav", 0.3);

            // Enter User Selection mode : next click on a character will assign them as the user
            IsUserSelectingCharacter = true;

            // Show a tooltip on User button
            User.ToolTip = "Click on a character to assign them as your character.";
        }

        #endregion

        #region TopBar and button interaction methods

        /// <summary>
        /// Handles a left click on the refresh button. Starts animation and triggers refresh logic.
        /// </summary>
        private async void Refresh_Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Refresh the state of all characters
            foreach (object? element in MainGrid.Children)
            {
                switch (element)
                {
                    // Reset character Labels to their original state, except for the user
                    case Label label when label.Name is not "WindowTitle" and not "Stats":
                        // Skip resetting the user's "YOU" label
                        if (UserCharacterLabel != null && label == UserCharacterLabel)
                        {
                            continue;
                        }

                        // Reset the previous user's "YOU" label
                        if (label != UserCharacterLabel)
                        {
                            label.Content = label.Name.Replace("_Label", "");
                            label.Foreground = Brushes.White;
                        }
                        break;

                    // Reset the Stats Label
                    case Label label when label.Name == "Stats":
                        UpdateStats();
                        break;

                    // Reset the Tag only (keep opacity < 1.0 to let the animation play), except for the user
                    case Border { Child: Image img }:
                        if (UserCharacterImage != null && img == UserCharacterImage)
                        {
                            // Skip resetting the user's tag
                            continue;
                        }

                        img.Tag = 0;
                        break;
                }
            }

            // Disable the button during animation
            Refresh_Button.IsEnabled = false;

            // Start the reset and the refresh icon animations
            RefreshButtonAnimation();
            ResetCharactersAnimation();

            // Play the refresh sound effect at 30% volume
            PlaySound("refresh.wav", 0.3);

            // Delay according to the animation duration
            await Task.Delay(500);

            // Enable the button after the animation
            Refresh_Button.IsEnabled = true;
        }


        /// <summary>
        /// Starts a single 360-degree rotation animation on the refresh icon
        /// </summary>
        private void RefreshButtonAnimation()
        {
            DoubleAnimation rotationAnimation = new()
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(0.5),
                RepeatBehavior = new RepeatBehavior(1)
            };

            RefreshRotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotationAnimation);
        }

        /// <summary>
        /// Handles a mouse click on the top bar to allow window dragging.
        /// </summary>
        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        /// <summary>
        /// Opens the GitHub repository in the default web browser.
        /// </summary>
        private void GitHub_Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/lucas-jammes/SusCompanion",
                UseShellExecute = true
            });

            PlaySound("browser.wav", 0.3);
        }

        /// <summary>
        /// Opens a PayPal donation tab in the default web browser.
        /// </summary>
        private void PayPal_Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.paypal.com/paypalme/lucasjammes",
                UseShellExecute = true
            });

            PlaySound("browser.wav", 0.3);
        }

        /// <summary>
        /// Closes the application window.
        /// </summary>
        private void Close_Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PlaySound("close.wav", 0.3);
            Close();
        }

        /// <summary>
        /// Toggles sound on or off and updates the sound icon path.
        /// </summary>
        private void Sound_Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsSoundEnabled)
            {
                PlaySound("sound-off.wav", 0.3);

                IsSoundEnabled = false;

                // Update the sound icon and visual state
                SoundPath.Data = Geometry.Parse("M3 11V13 M6 11V13 M9 11V13 M12 10V14 M15 11V13 M18 11V13 M21 11V13");
                Sound_Button.Opacity = 0.5;
            }
            else
            {
                IsSoundEnabled = true;

                PlaySound("sound-on.wav", 0.3);

                // Update the sound icon and visual state
                SoundPath.Data = Geometry.Parse("M3 11V13 M6 8V16 M9 10V14 M12 7V17 M15 4V20 M18 9V15 M21 11V13");
                Sound_Button.Opacity = 1.0;
            }

            // Persist the sound setting for future sessions
            Properties.Settings.Default.IsSoundEnabled = IsSoundEnabled;
            Properties.Settings.Default.Save();
        }

        #endregion

        #region Utility methods

        /// <summary>
        /// Updates the Stats label with counts of ALIVE, SAFE, SUS, and DEAD characters.
        /// </summary>
        private void UpdateStats()
        {
            // Get all Image controls inside MainGrid
            IEnumerable<Image> images = MainGrid.Children
                .OfType<Border>()
                .Select(b => b.Child)
                .OfType<Image>();

            int aliveCount = 0;
            int safeCount = 0;
            int susCount = 0;
            int deadCount = 0;

            foreach (Image img in images)
            {
                // Skip counting the User selector image
                if (img.Name == "User")
                    continue;

                bool isDead = img.Opacity < 1.0;
                int state = img.Tag as int? ?? 0;

                // If the character is dead, increment the dead count
                if (isDead)
                {
                    deadCount++;
                }
                // Otherwise, increment the safe or sus count
                else
                {
                    aliveCount++;

                    if (state == 1)
                    {
                        safeCount++;
                    }
                    else if (state == 2)
                    {
                        susCount++;
                    }
                }
            }

            // Build the stats display using a TextBlock so colors are preserved
            TextBlock statsBlock = new()
            {
                TextAlignment = TextAlignment.Center
            };

            statsBlock.Inlines.Add(new Run($"{aliveCount} Alive") { Foreground = Brushes.White });
            statsBlock.Inlines.Add(new Run("   -   "));
            statsBlock.Inlines.Add(new Run($"{safeCount} Safe") { Foreground = Brushes.LimeGreen });
            statsBlock.Inlines.Add(new Run("   -   "));
            statsBlock.Inlines.Add(new Run($"{susCount} Sus") { Foreground = Brushes.Red });
            statsBlock.Inlines.Add(new Run("   -   "));
            statsBlock.Inlines.Add(new Run($"{deadCount} Dead") { Foreground = Brushes.DarkSlateGray });

            // Update the Stats label with the new content
            Stats.Content = statsBlock;
        }

        /// <summary>
        /// Plays a preloaded sound with specified volume. Supports overlapping playback without interrupting other sounds.
        /// </summary>
        /// <param name="resourceName">The embedded sound filename (e.g., "dead.wav").</param>
        /// <param name="volume">Playback volume from 0.0 to 1.0.</param>
        private void PlaySound(string resourceName, double volume)
        {
            if (!IsSoundEnabled)
            {
                return;
            }

            if (!_soundPlayers.TryGetValue(resourceName, out MediaPlayer? originalPlayer))
            {
                _ = MessageBox.Show($"Sound {resourceName} not preloaded.");
                return;
            }

            // Create a clone of the original MediaPlayer for concurrent playback
            MediaPlayer player = new();
            player.Open(originalPlayer.Source);
            player.Volume = volume;

            player.Play();

            // Cleanup after playback ends
            player.MediaEnded += (s, e) =>
            {
                player.Close();
            };
        }

        /// <summary>
        ///  Resets the animation of all characters to their initial state.
        /// </summary>
        private void ResetCharactersAnimation()
        {
            IEnumerable<Image> images = MainGrid.Children
                .OfType<Border>()
                .Select(b => b.Child)
                .OfType<Image>();

            foreach (Image img in images)
            {
                if (img.Opacity < 1.0)
                {
                    // Set character to Alive state
                    img.Opacity = 1.0;

                    // Start Fade In animation (opacity from 0.15 to 1.0 in 0.5 seconds)
                    DoubleAnimation fadeIn = new()
                    {
                        From = 0.15,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(0.5),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                        FillBehavior = FillBehavior.Stop
                    };

                    // Begin the animation
                    img.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                }
            }

            UpdateStats();
        }

        /// <summary>
        /// Returns the associated color for a character name.
        /// </summary>
        private Brush GetCharacterColor(string characterName)
        {
            return characterName switch
            {
                "Red" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C61111")),
                "Blue" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#132ED2")),
                "Green" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#11802D")),
                "Pink" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EE54BB")),
                "Orange" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F07D0D")),
                "Yellow" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6F657")),
                "Black" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F474E")),
                "White" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D7E1F1")),
                "Purple" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B2FBC")),
                "Brown" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#71491E")),
                "Cyan" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38E2DD")),
                "Lime" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#50F039")),
                "Maroon" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B2B3C")),
                "Rose" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECC0D3")),
                "Banana" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFEBE")),
                "Gray" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#708495")),
                "Tan" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#928776")),
                "Coral" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EC7578")),
                _ => Brushes.White  // Default fallback
            };
        }

        #endregion
    }
}
