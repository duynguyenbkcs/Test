using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using ZXing;

namespace EAR.QRCode
{
    public class QRCodeReader : MonoBehaviour
    {
        public AspectRatioFitter fit;
        public RawImage background;

        public event Action<string> QRCodeRecognizedEvent;

        private WebCamTexture webCamTexture;
        private IBarcodeReader barcodeReader;
        private bool camAvailable;
        private Thread currentThread;
        private string result = "";
        void Start()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.Log("No camera");
                camAvailable = false;
                return;
            }

            for (int i = 0; i < devices.Length; i++)
            {
                if (!devices[i].isFrontFacing)
                {
                    webCamTexture = new WebCamTexture(devices[i].name, Screen.width, Screen.height);
                }
            }

            if (webCamTexture == null)
            {
                webCamTexture = new WebCamTexture(devices[0].name, Screen.width, Screen.height);
                Debug.Log("No back camera found, using front camera instead.");
            }
            barcodeReader = new BarcodeReader();
            barcodeReader.Options.PossibleFormats = new List<BarcodeFormat>();
            barcodeReader.Options.PossibleFormats.Add(BarcodeFormat.QR_CODE);
            //barcodeReader.Options.TryHarder = false;
            background.texture = webCamTexture;
            camAvailable = true;
            StartCoroutine(PlayWebCam());
            StartCoroutine(CheckQRCode());
        }

        private IEnumerator PlayWebCam()
        {
            webCamTexture.Play();
            if (!webCamTexture.isPlaying)
            {
                yield return new WaitForSeconds(0.2f);
                yield return StartCoroutine(PlayWebCam());
            }
        }

        void Update()
        {
            if (camAvailable && webCamTexture.isPlaying)
            {
                float ratio = (float)webCamTexture.width / webCamTexture.height;
                fit.aspectRatio = ratio;

                float scaleY = webCamTexture.videoVerticallyMirrored ? -1f : 1f;
                background.rectTransform.localScale = new Vector3(1, scaleY, 1);

                int orient = -webCamTexture.videoRotationAngle;
                background.rectTransform.localEulerAngles = new Vector3(0, 0, orient);
            }

            lock(result)
            {
                if (result != "")
                {
                    Debug.Log(result);
                    QRCodeRecognizedEvent?.Invoke(result);
                    result = "";
                }
            }
        }

        public void StopScan()
        {
            webCamTexture.Stop();
        }

        private IEnumerator CheckQRCode()
        {
            while (true)
            {
                if (camAvailable)
                {
                    if (webCamTexture.isPlaying)
                    {
                        lock (result)
                        {
                            if ((currentThread == null || !currentThread.IsAlive) && result == "")
                            {
                                StartNewBarcodeReadThread(webCamTexture.GetPixels32(), webCamTexture.width, webCamTexture.height);
                            }
                        }
                        yield return new WaitForSeconds(1.5f);
                    } else
                    {
                        yield return new WaitForSeconds(1);
                    }
                    
                } else {
                    break;
                }
            }
        }
        private void StartNewBarcodeReadThread(Color32[] color32s, int width, int height)
        {

            currentThread = new Thread(() =>
            {
                Result barcodeResult = barcodeReader.Decode(color32s, width, height);
                if (barcodeResult != null)
                {
                    lock(result)
                    {
                        result = barcodeResult.Text;
                    }
                }
            });
            currentThread.Start();
        }
    }
}

