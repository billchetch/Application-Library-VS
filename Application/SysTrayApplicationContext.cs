using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Application
{
    /*
     * Class for Form based sys tray icon apps
     * */

    abstract public class SysTrayApplicationContext : ApplicationContext
    {
        private Container _components;
        protected NotifyIcon NotifyIcon;
        private Form _mainForm;
        
        public SysTrayApplicationContext()
        {
            InitializeContext();
        }

        virtual protected void InitializeContext()
        {
            _components = new Container();
            NotifyIcon = new NotifyIcon(_components);
            NotifyIcon.Visible = true;
            NotifyIcon.DoubleClick += new EventHandler(this.notifyIcon_DoubleClick);
            
            NotifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu();
            AddNotifyIconContextMenuItem("Open...", "Open");
            AddNotifyIconContextMenuItem("Exit");
        }

        protected void AddNotifyIconContextMenuItem(String text, String tag = null)
        {
            var mi = new System.Windows.Forms.MenuItem();
            mi.Text = text;
            mi.Tag = tag == null ? mi.Text : tag;
            mi.Click += new System.EventHandler(this.contextMenuItem_Click);
            NotifyIcon.ContextMenu.MenuItems.Add(mi);
        }

        abstract protected Form CreateMainForm();

        virtual protected void contextMenuItem_Click(Object sender, EventArgs e)
        {
            MenuItem mi = (MenuItem)sender;
            switch (((String)mi.Tag).Trim().ToUpper())
            {
                case "EXIT":
                    System.Windows.Forms.Application.Exit();
                    break;

                case "OPEN":
                    OpenMainForm();
                    break;
            }
        }

        private void OpenMainForm()
        {
            if (_mainForm == null)
            {
                _mainForm = CreateMainForm();
                _mainForm.FormClosed += mainForm_FormClosed;
            }
            _mainForm.Show();
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            OpenMainForm();
        }

        private void mainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _mainForm = null;
        }

        protected override void ExitThreadCore()
        {
            if (_mainForm != null) { _mainForm.Close(); }
            NotifyIcon.Visible = false; // should remove lingering tray icon!
            base.ExitThreadCore();
        }
    }
}
