using UnityEngine;
using System.Collections;
using MoleculeLogic;

namespace MoleculeUI
{
    public class CruveMainPanel : BasePanelEx<CruveMainPanel>
    {
        private MoleculeFactory mf;

        private void Awake()
        {
            base.Awake();

            mf = MoleculeFactory.Instance;
        }

        public override void OnShow()
        {
    	    base.OnShow();

            if (SelectPanel.Instance)
                SelectPanel.Instance.SetVisible(false);

            RefreshMoleculeFactory();
        }

        public override void OnHide()
        {
            base.OnHide();

            if (SelectPanel.Instance)
                SelectPanel.Instance.SetVisible(true);

            if (KeyboardPanelEx.Instance)
                KeyboardPanelEx.Instance.SetVisible(false);

            RefreshMoleculeFactory();
        }


        private void RefreshMoleculeFactory()
        {
            bool vis = DynamicPanel.Instance.GetAllVis();
            //if (vis)
            //{
            //    mf.transform.position = new Vector3(5000, 0, 0);
            //}
            //else
            {

                if (!mf)
                    return;
                mf.transform.position = this.transform.position - this.transform.forward * 3.0f -
                    this.transform.right * 0.2f - this.transform.up *  0.5f;
                Vector3 oldangle = mf.transform.eulerAngles;
                mf.transform.forward = this.transform.forward;
                Vector3 newangle = mf.transform.eulerAngles;
                newangle.x = oldangle.x;
                newangle.z = oldangle.z;
                mf.transform.eulerAngles = newangle;
            }
        }
    }
}