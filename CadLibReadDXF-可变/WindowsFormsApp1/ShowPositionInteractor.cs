using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WW.Actions;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public class ShowPositionInteractor : Interactor
    {
        public override bool ProcessKeyDown(
            CanonicalMouseEventArgs e,
            System.Windows.Input.Key key,
            System.Windows.Input.ModifierKeys modifierKeys,
            InteractionContext context
            )
        {
            MessageBox.Show(e.GetWcsPosition(context).ToString());
            return true;
        }
        
    }
}
