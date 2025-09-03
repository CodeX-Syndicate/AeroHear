using System;
using System.Drawing;
using System.Windows.Forms;
using AeroHear.Audio;

namespace AeroHear.Forms
{
    /// <summary>
    /// Dialog that explains the virtual audio device functionality
    /// </summary>
    public partial class VirtualDeviceHelpForm : Form
    {
        public VirtualDeviceHelpForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "AeroHear - Mode Système Audio";
            Size = new Size(600, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                AutoScroll = true
            };
            Controls.Add(panel);

            var titleLabel = new Label
            {
                Text = "Mode Système Audio - Comment ça marche",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(0, 0),
                Size = new Size(540, 30),
                ForeColor = Color.DarkBlue
            };
            panel.Controls.Add(titleLabel);

            var explanationText = new RichTextBox
            {
                Location = new Point(0, 40),
                Size = new Size(540, 280),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                Font = new Font("Segoe UI", 10)
            };

            explanationText.Text = @"🎵 Mode Système Audio d'AeroHear

Quand vous activez le mode système, AeroHear :

✅ Capture en temps réel tout l'audio de votre PC
✅ Diffuse simultanément vers tous vos périphériques Bluetooth sélectionnés
✅ Applique les réglages de volume et délai configurés
✅ Fonctionne avec toutes les applications : YouTube, Spotify, jeux, etc.

📝 Instructions d'utilisation :

1. Sélectionnez vos périphériques Bluetooth
2. Ajustez les volumes et délais si nécessaire
3. Cochez ""Mode système"" pour démarrer la capture
4. Lancez n'importe quelle application audio sur votre PC
5. L'audio sera diffusé vers tous vos périphériques !

⚠️ Important :
• Le mode système remplace la lecture de fichiers
• Pour écouter des fichiers, désactivez d'abord le mode système
• Évitez les boucles audio en désactivant le haut-parleur principal

🔧 Intégration Windows :
AeroHear utilise la capture audio système (WASAPI) pour fonctionner avec l'ensemble de votre ordinateur. Pour une intégration complète dans les Paramètres Son de Windows, il faudrait développer un pilote audio virtuel Windows.";

            panel.Controls.Add(explanationText);

            var statusButton = new Button
            {
                Text = "Statut du Périphérique Virtuel",
                Location = new Point(0, 330),
                Size = new Size(200, 30)
            };
            statusButton.Click += StatusButton_Click;
            panel.Controls.Add(statusButton);

            var adminButton = new Button
            {
                Text = "Redémarrer en Admin",
                Location = new Point(210, 330),
                Size = new Size(150, 30),
                Enabled = !VirtualDeviceRegistration.IsRunAsAdministrator()
            };
            adminButton.Click += AdminButton_Click;
            panel.Controls.Add(adminButton);

            var closeButton = new Button
            {
                Text = "Fermer",
                Location = new Point(450, 330),
                Size = new Size(80, 30),
                DialogResult = DialogResult.OK
            };
            panel.Controls.Add(closeButton);

            AcceptButton = closeButton;
        }

        private void StatusButton_Click(object sender, EventArgs e)
        {
            var status = VirtualDeviceRegistration.GetRegistrationStatus();
            MessageBox.Show(status, "Statut du Périphérique Virtuel", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AdminButton_Click(object sender, EventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Redémarrer AeroHear avec des privilèges administrateur ?\n\n" +
                    "Cela permettra d'enregistrer complètement le périphérique virtuel.",
                    "Privilèges Administrateur",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    VirtualDeviceRegistration.RestartAsAdministrator();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}