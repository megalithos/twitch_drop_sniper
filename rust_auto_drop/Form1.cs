using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace rust_auto_drop
{
    public partial class Form1 : Form
    {
        private string token;
        private int stopAfterSeconds = 0;
        private int maxStreamsAtOnce;
        bool hiddenMode;
        Thread mainThread;

        public Form1()
        {
            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
            InitializeComponent();
            Thread.CurrentThread.IsBackground = false;
            textBox2.Text = "0"; // set to 0 by default
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// Start program if all input is good.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if (mainThread != null) return;
            if (string.IsNullOrEmpty(token))
            {
                DisplayError("You must specify a token!");
                return;
            }
            try
            {
                stopAfterSeconds = Convert.ToInt32(textBox2.Text);
            } catch (Exception) { DisplayError("Input was not in correct format");  return; }
            
            mainThread = new Thread(() => Program.MainLoop(token, hiddenMode, maxStreamsAtOnce));
            mainThread.Start();

            if (stopAfterSeconds != 0)
            {
                Timer t = new Timer(stopAfterSeconds * 1000);
                t.Elapsed += OnTimedEvent;
                t.Enabled = true;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            token = textBox1.Text;
        }

        public void DisplayError(string errorMsg)
        {
            // Initializes the variables to pass to the MessageBox.Show method.
            string caption = "Error";
            MessageBoxButtons buttons = MessageBoxButtons.OK;
            DialogResult result;
            // Displays the MessageBox.
            result = MessageBox.Show(errorMsg, caption, buttons, MessageBoxIcon.Error);
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            
        }

        public void Exit()
        {
            Program.QuitDriver();
            Program.QuitStreamWatcherDriver();
            if (mainThread!= null) mainThread.Abort();
            Program.CleanupAllChromedriverProcesses();
            Environment.Exit(0);
        }

        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            Console.WriteLine("tick");
            Exit();
        }


        private void OnApplicationExit(object sender, EventArgs e)
        {
            Exit();
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click_1(object sender, EventArgs e)
        {

        }

        public void SetStatus(string status)
        {
            MethodInvoker inv = delegate { label3.Text = "status: " + status; }; // idk, works though since can't update label from another thread
            
            this.Invoke(inv);
        }

        public void AppendWatchingStreamersText(string text)
        {
            MethodInvoker inv = delegate { richTextBox1.AppendText(text); }; // idk, works though since can't update label from another thread

            this.Invoke(inv);
        }

        public void ClearWatchingStreamersText()
        {
            MethodInvoker inv = delegate { richTextBox1.Text = string.Empty; }; // idk, works though since can't update label from another thread

            this.Invoke(inv);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            hiddenMode = checkBox1.Checked;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void label6_Click(object sender, EventArgs e)
        {

        }
    }
}
