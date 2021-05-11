using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;

namespace LXFML_To_OBJ
{
    public partial class MainForm : Form
    {
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public MainForm()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void MinimizeButton_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void HeaderPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void AssetBrowseButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "lif files (*.lif)|*.lif";

            if(ofd.ShowDialog() == DialogResult.OK)
            {
                AssetsTextBox.Text = ofd.FileName;
            }
        }

        private void LXFMLBrowseButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "lxfml files (*.lxfml)|*.lxfml";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                LXFMLTextBox.Text = ofd.FileName;
            }
        }

        private void ConvertButton_Click(object sender, EventArgs e)
        {
            if (NotValidInput())
                return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "obj files (*.obj)|*.obj";

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                string Assets = AssetsTextBox.Text;
                string LXFML = LXFMLTextBox.Text;
                string Output = sfd.FileName;

                List<GB10> Parts = new List<GB10>();

                using (XmlReader reader = XmlReader.Create(LXFML))
                {
                    reader.ReadToFollowing("Part");
                    do
                    {
                        //-----------------------------
                        //----- Initialize G File -----
                        //-----------------------------

                        GB10 g = new GB10(Assets, reader.GetAttribute("designID"), reader.GetAttribute("materialID"));

                        if (reader.ReadToDescendant("Decoration"))
                        {
                            g.SetTexture(Assets, reader.GetAttribute("decorationID"));
                        }

                        //-------------------------
                        //----- Rotate Meshes -----
                        //-------------------------

                        float angle = Convert.ToSingle(reader.GetAttribute("angle"));
                        float ax = Convert.ToSingle(reader.GetAttribute("ax"));
                        float ay = Convert.ToSingle(reader.GetAttribute("ay"));
                        float az = Convert.ToSingle(reader.GetAttribute("az"));
                        g.Rotate(angle, ax, ay, az);

                        //----------------------------
                        //----- Transform Meshes -----
                        //----------------------------

                        float xOffset = Convert.ToSingle(reader.GetAttribute("tx"));
                        float yOffset = Convert.ToSingle(reader.GetAttribute("ty"));
                        float zOffset = Convert.ToSingle(reader.GetAttribute("tz"));
                        g.Offset(xOffset, yOffset, zOffset);

                        Parts.Add(g);

                    } while (reader.ReadToFollowing("Part"));
                }

                GB10.WriteOBJ(Parts, Output);
            }
        }

        private bool NotValidInput()
        {
            return string.IsNullOrWhiteSpace(AssetsTextBox.Text) || string.IsNullOrWhiteSpace(LXFMLTextBox.Text);
        }
    }
}
