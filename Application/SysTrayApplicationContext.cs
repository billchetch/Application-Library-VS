using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Application
{
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
        }

        abstract protected Form CreateMainForm();
        
        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            if (_mainForm == null)
            {
                _mainForm = CreateMainForm();
                _mainForm.FormClosed += mainForm_FormClosed;
            }
            _mainForm.Show();
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
