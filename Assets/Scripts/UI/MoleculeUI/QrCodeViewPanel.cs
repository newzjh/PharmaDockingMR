
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnifiedInput;
using MoleculeLogic;
using ZXing;
using ZXing.QrCode;

namespace MoleculeUI
{

    public class QrCodeViewPanel : BasePanelEx<QrCodeViewPanel>
    {
        Transform timg;
        public override void Awake()
        {
            base.Awake();
            timg = this.transform.Find("QrCode");
        }

        private MoleculeController mc;

        public Dictionary<string, iDockConfItem> cfgItems = new Dictionary<string, iDockConfItem>();

        public void Show(MoleculeController _mc)
        {
            mc = _mc;
            SetVisible(true);

            if (timg)
            {
                Image img = timg.GetComponent<Image>();
                if (img)
                {
                    int requestw = 1024;
                    int requesth = 1024;
                    Texture2D tex = Encode(mc.url, requestw, requesth);
                    Sprite tempSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0, 0));
                    img.sprite = tempSprite;
                    img.color = Color.white;
                }
            }

            if (mc != null)
            {
                Vector3 pos = mc.mol.transform.position;
                Transform tcam = Camera.main.transform;
                pos -= tcam.up * 0.05f;
                pos -= tcam.forward * 0.3f;
                this.transform.position = pos;
                this.transform.forward = tcam.transform.forward;
            }
        }

        public void Hide()
        {
            if (timg)
            {
                Image img = timg.GetComponent<Image>();
                if (img)
                {
                    img.color = new Color(0, 0, 0, 0);
                    img.sprite = null;
                }
            }

            SetVisible(false);

            mc = null;

            System.GC.Collect();
        }

        private static Texture2D Encode(string textForEncoding, int width, int height)
        {
            Texture2D encoded = new Texture2D(width, height);
            BarcodeWriter writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = height,
                    Width = width
                }
            };
            Color32[] data = writer.Write(textForEncoding);
            encoded.SetPixels32(data);
            encoded.Apply();
            return encoded;
        }

        public override void OnClick(GameObject b)
        {
            if (b.name.StartsWith("Button_OK"))
            {
                OnOK();
            }
        }


        private void OnOK()
        {
            Hide();
        }



    }

}