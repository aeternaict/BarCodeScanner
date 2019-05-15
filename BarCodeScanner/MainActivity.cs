using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V7.App;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Java.Interop;
using ZXing.Mobile;


namespace BarCodeScanner
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true,
              ScreenOrientation = ScreenOrientation.Landscape,
              ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    public class MainActivity : AppCompatActivity
    {
        const int RequestLocationId = 0;

        private PowerManager _powerManager;
        private PowerManager.WakeLock _wakeLock;

        public bool bGranted = false;
        public string sWebUrl = "";

        protected override void OnCreate(Bundle savedInstanceState)
        {
            RequestPermission();

            _powerManager = (PowerManager)GetSystemService(PowerService);
            _wakeLock = _powerManager.NewWakeLock(WakeLockFlags.Full, "@string/app_name");
            
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            MobileBarcodeScanner.Initialize(Application);

            WebView webView = FindViewById<WebView>(Resource.Id.webView);

            webView.SetWebChromeClient(new WebChromeClient());
            webView.SetWebViewClient(new WebViewClient());
            webView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
            webView.Settings.JavaScriptEnabled = true;
            webView.Settings.AllowFileAccessFromFileURLs = true;
            webView.Settings.AllowUniversalAccessFromFileURLs = true;
            webView.Settings.AllowFileAccess = true;
            webView.ClearHistory();
            webView.ClearCache(true);
            WebSettings webSettings = webView.Settings;
            webSettings.SetAppCacheEnabled(false);

            webView.AddJavascriptInterface(new QRScannerJSInterface(webView, this), "CSharpQRInterface");
            webView.LoadUrl("file:///android_asset/TestBarCode/testbarcode.html");
            //webView.LoadUrl("http://IP-YOURWEBSERVER/testbarcode.html");    //if you want to test on your web server, copy testbarcode.html on your web server
        }

        public void CopyFileFromAssetsToFolder(string dstPath, string AssetsFileName)
        {
            System.IO.Directory.CreateDirectory(dstPath);
            if (!System.IO.File.Exists(dstPath + "/" + AssetsFileName))
            {
                using (BinaryReader br = new BinaryReader(Assets.Open(AssetsFileName)))
                {
                    using (BinaryWriter bw = new BinaryWriter(new FileStream(dstPath + "/" + AssetsFileName, FileMode.Create)))
                    {
                        byte[] buffer = new byte[2048];
                        int len = 0;
                        while ((len = br.Read(buffer, 0, buffer.Length)) > 0)
                            bw.Write(buffer, 0, len);
                    }
                }
            }
        }

        public void ReadUrl(string Path, string FileName)
        {
            if (System.IO.File.Exists(Path + "/" + FileName))
            {
                string[] lines = System.IO.File.ReadAllLines(Path + "/" + FileName);
                sWebUrl = lines[0];
            }
            else
                Message("URL not found!");
        }

        public void Message(string sMex)
        {
            var dlg = new Android.App.AlertDialog.Builder(this);

            dlg.SetTitle("")
                   .SetMessage(sMex)
                   .SetPositiveButton("OK", delegate { System.Console.WriteLine("OK"); });

            RunOnUiThread(() => dlg.Create().Show());
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                /*
                case Resource.Id.action_valida:
                    return true;
                */
            }

            return base.OnOptionsItemSelected(item);
        }


        public void RequestPermission()
        {
            string[] PermissionsArray =
            {
              Android.Manifest.Permission.ReadExternalStorage,
              Android.Manifest.Permission.WriteExternalStorage
            };

            if ((int)Build.VERSION.SdkInt >= 23)
            {
                if (Android.Support.V4.Content.ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.ReadExternalStorage) == (int)Permission.Granted)
                {
                    bGranted = true;
                }
                else
                {
                    //set alert for executing the task
                    Android.App.AlertDialog.Builder alert = new Android.App.AlertDialog.Builder(this);
                    alert.SetTitle("Storage permission");
                    alert.SetMessage("Allow this app to use storage device?");
                    alert.SetPositiveButton("ALLOW", (senderAlert, args) =>
                    {
                        ActivityCompat.RequestPermissions(this, PermissionsArray, (int)Permission.Granted);
                    });

                    alert.SetNegativeButton("EXIT APP", (senderAlert, args) =>
                    {
                        //Toast.MakeText(this, "EXIT", ToastLength.Short).Show();
                        KillApp(2000);
                    });

                    Dialog dialog = alert.Create();
                    dialog.Show();
                }
            }
            else
                bGranted = true;
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            switch (requestCode)
            {
                case RequestLocationId:
                    {
                        if (grantResults[0] == Permission.Granted)
                        {
                            //Permission granted
                            Toast.MakeText(this, "PERMISSION GRANTED", ToastLength.Long).Show();
                            //RunOnUiThread(() => Toast.MakeText(ApplicationContext, "App will restart. Please wait...", ToastLength.Long).Show());
                            Intent RestartApp = new Intent(this, typeof(MainActivity));
                            RestartApp.AddFlags(ActivityFlags.ClearTop);
                            StartActivity(RestartApp);
                        }
                        else
                        {
                            //Permission Denied
                            Toast.MakeText(this, "PERMISSION DENIED", ToastLength.Long).Show();
                            KillApp(2000);
                        }
                    }
                    break;
            }
        }

        public void KillApp(int iMilliSeconds)
        {
            Thread.Sleep(iMilliSeconds);
            Process.KillProcess(Process.MyPid());
            System.Environment.Exit(0);
        }

        protected override void OnDestroy()
        {
            if (_wakeLock.IsHeld)
                _wakeLock.Release();

            base.OnDestroy();
        }
    }

    public class QRScannerJSInterface : Java.Lang.Object
    {
        QRScanner qrScanner;
        WebView webViewLoc;

        Activity activityObj;


        public QRScannerJSInterface(WebView webView, Activity activityObj)
        {
            webViewLoc = webView;
            qrScanner = new QRScanner();

            this.activityObj = activityObj;
        }

        [Export("ScanQR")]
        [JavascriptInterface]
        public void ScanQR()
        {
            qrScanner.ScanQR().ContinueWith((t) =>
            {
                //Call the Javascript method here with "result" as its parameter to get the scanned value
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    //Attention: it is necessary to use the activityObj context of the main activity to execute the javascript code in the webview defined in the UI
                    //otherwise the javascript code is not executed
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
                    {
                        activityObj.RunOnUiThread(() => {
                            webViewLoc.EvaluateJavascript("javascript:getQRValue('" + t.Result + "');", null);
                        });
                    }
                    else
                    {
                        activityObj.RunOnUiThread(() => {
                            webViewLoc.LoadUrl("javascript:getQRValue('" + t.Result + "');");
                        });
                    }
                }
            });
            
        }
    }

    class QRScanner
    {
        MobileBarcodeScanner scanner;

        public QRScanner()
        {
            scanner = new MobileBarcodeScanner();
        }

        public async Task<string> ScanQR()
        {
            scanner.UseCustomOverlay = false;
            scanner.TopText = "Scan barcode";
            var result = await scanner.Scan();
            return result.ToString();
        }
    }
}