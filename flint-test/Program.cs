using System;
using flint;
using SharpMenu;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;

namespace flint_test
{
    /// <summary> Demonstrates and tests the functionality of the Flint library.
    /// </summary>
    class Program
    {
        static void p(string[] args)
        {

            Dictionary<string,string> argmap = new Dictionary<string,string>();
            for (int i=0;i<args.Length;++i)
            {
                var arg = args[i];
                if (arg == "--test")
                {
                    argmap["test"] = "yes";
                }
                else if (arg == "--pebble_id")
                {
                    var addr = i+1<args.Length?args[++i]:null;
                    if (addr.Length > 4)
                    {
                        // assume BT addr or shortcut
                        addr = addr.Replace(":","");
                        addr = addr.Substring(addr.Length-4,4);
                    }
                    argmap["pebble_id"] = addr;
                }
                else if (arg == "--nolaunch")
                {
                    argmap["no_launch"] = "";
                }
                else if (arg == "load")
                {
                    argmap["load"] = i + 1 < args.Length ? args[++i] : null;
                }
                else if (arg == "reinstall")
                {
                    argmap["load"] = i + 1 < args.Length ? args[++i] : null;
                    argmap["uninstall"] = argmap["load"];
                }
            }
            if (argmap["test"] != null)
            {
                TestPack();
                return;
            }
            var pebble = Pebble.GetPebble(argmap["pebble_id"]);
            try
            {
                pebble.Connect();
            }
            catch (System.IO.IOException e)
            {
                Console.Write("Connection failed: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Press enter to exit.");
                Console.ReadLine();
                return;
            }
            Console.WriteLine("Successfully connected!");
            Console.WriteLine(pebble);


            if (argmap["load"] != null)
            {
                PebbleBundle pb = new PebbleBundle(argmap["load"]);
                var uuid = pb.Application.UUID;
                var apps = pebble.GetAppbankContents().AppBank.Apps;
                var app = apps.Find(a => Encoding.ASCII.GetString(BitConverter.GetBytes(a.ID)) == uuid);

            }
        }
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                p(args);
                return;
            }
            Pebble pebble;
            SharpMenu<Action> menu;
            SharpMenu<Pebble> pebblemenu;

            Console.WriteLine("Welcome to the Flint test environment.  "
                + "Please remain seated and press enter to autodetect paired Pebbles.");
            Console.ReadLine();

            try 
            {
                List<Pebble> peblist = Pebble.DetectPebbles();
                switch (peblist.Count)
                {
                    case 0:
                        Console.WriteLine("No Pebbles found.  Press enter to exit.");
                        Console.ReadLine();
                        return;
                    case 1: 
                        pebble = peblist[0];
                        break;
                    default:
                        pebblemenu = new SharpMenu<Pebble>();
                        foreach (Pebble peb in Pebble.DetectPebbles()) 
                        {
                            pebblemenu.Add(peb);
                        }
                        pebblemenu.WriteMenu();
                        pebble = pebblemenu.Prompt();
                        break;
                }
            }
            catch (PlatformNotSupportedException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Press enter to exit.");
                Console.ReadLine();
                return;
            }
            
            try
            {
                pebble.Connect();
            }
            catch (System.IO.IOException e)
            {
                Console.Write("Connection failed: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Press enter to exit.");
                Console.ReadLine();
                return;
            }
            Console.WriteLine("Successfully connected!");
            Console.WriteLine(pebble);

            menu = new SharpMenu<Action>();
            menu.Add(() => pebble.Ping(235), "Send the Pebble a ping");
            menu.Add(() => pebble.NotificationSMS("+3278051200", "It's time."), "Send an SMS notification");
            menu.Add(() => pebble.NotificationMail("Your pal", "URGENT NOTICE", "There is a thing you need to do. Urgently."),
                "Send an email notification");
            menu.Add(() => pebble.SetNowPlaying("That dude", "That record", "That track"), "Send some metadata to the music app");
            menu.Add(() => pebble.BadPing(), "Send a bad ping to trigger a LOGS response");
            menu.Add(() => Console.WriteLine(pebble.GetTime().Time), "Get the time from the Pebble");
            menu.Add(() => pebble.SetTime(DateTime.Now), "Sync Pebble time");
            menu.Add(() => Console.WriteLine(pebble.GetAppbankContents().AppBank), "Get the contents of the app bank");
            menu.Add(() => DeleteApp(pebble), "Delete an app from the Pebble");
            menu.Add(() => AddApp(pebble), "Add an app");
            menu.Add(() => pebble.Disconnect(), "Exit");

            pebble.OnDisconnect += pebble_OnDisconnect;

            pebble.MessageReceived += pebble_MessageReceived;
            // Subscribe to specific events
            pebble.LogReceived += pebble_LogReceived;
            pebble.PingReceived += pebble_PingReceived;
            pebble.MediaControlReceived += pebble_MediaControlReceived;
            // Subscribe to an event for a particular endpoint
            pebble.RegisterEndpointCallback(Pebble.Endpoints.PING, pingReceived);

            pebble.GetVersion();
            Console.WriteLine(pebble.Firmware);
            Console.WriteLine(pebble.RecoveryFirmware);

            while (pebble.Alive)
            {
                menu.WriteMenu();
                Action act = menu.Prompt();
                // To account for disconnects during the prompt:
                if (pebble.Alive) act();
            }
        }
        static void TestPack()
        {
#if DEBUG
            if (ConfigurationManager.AppSettings["watch-dir"] != null)
            {
                PebbleBundle bundle = new PebbleBundle(Path.Combine(ConfigurationManager.AppSettings["watch-dir"],"brains.pbw"));
            }

            var items = new object[] { (sbyte)-11, -123, UInt32.MaxValue, (byte)244, (uint)0,short.MinValue,ushort.MaxValue,Int32.MinValue,UInt32.MaxValue,"12345","1234","123456","DOES_NOT_MATTER","a" };
            var data = Util.Pack("!biIBIhHlL5s5s5s0ss",items);
            var ritems = Util.Unpack("!biIBIhHlL5s5s5s0ss", data);
            if (items.Length != ritems.Length) throw new Exception();
            if ((sbyte)items[0] != (sbyte)ritems[0]) throw new Exception();
            if ((int)items[1] != (int)ritems[1]) throw new Exception();
            if ((uint)items[2] != (uint)ritems[2]) throw new Exception();
            if ((byte)items[3] != (byte)ritems[3]) throw new Exception();
            if ((uint)items[4] != (uint)ritems[4]) throw new Exception();
            if ((short)items[5] != (short)ritems[5]) throw new Exception();
            if ((ushort)items[6] != (ushort)ritems[6]) throw new Exception();
            if ((int)items[7] != (int)ritems[7]) throw new Exception();
            if ((uint)items[8] != (uint)ritems[8]) throw new Exception();
            if ((string)items[9] != (string)ritems[9]) throw new Exception(); // normal string
            if ((string)items[10] != (string)ritems[10]) throw new Exception(); // shorter than expected
            if (((string)items[11]).Substring(0,5) != (string)ritems[11]) throw new Exception(); // longer than expected
            if ("" != (string)ritems[12]) throw new Exception(); // empty string
            if ((string)items[13] != (string)ritems[13]) throw new Exception(); // implied string length of 1

            items = new object[] { Byte.MaxValue, (byte)100,ushort.MaxValue };
            data = Util.Pack("!2BH", items);
            ritems = Util.Unpack("!2BH", data);
            if ((byte)items[0] != (byte)ritems[0]) throw new Exception();
            if ((byte)items[1] != (byte)ritems[1]) throw new Exception();
            if ((ushort)items[2] != (ushort)ritems[2]) throw new Exception();
#endif
        }

        static void AddApp(Pebble pebble)
        {
            var watchdir = ConfigurationManager.AppSettings["watch-dir"];
            if (watchdir == null)
            {
                Console.WriteLine("Missing .config entry for 'watch-dir'");
                return;
            }
            if (!Directory.Exists(watchdir))
            {
                Console.WriteLine("watch-dir not found: {0}", watchdir);
                return;
            }
            var appbank = pebble.GetAppbankContents().AppBank;
            var applist = appbank.Apps;
            if (applist.Count == appbank.Size)
            {
                Console.WriteLine("All {0} banks are full", appbank.Size);
                return;
            }
            try
            {
                Console.WriteLine("Choose an app to install");
                var watches = Directory.GetFiles(watchdir,"*.pbw");
                var result = SharpMenu<string>.WriteAndPrompt(watches);
                pebble.InstallApp(Path.Combine(watchdir,result),applist.Count);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static void DeleteApp(Pebble pebble)
        {
            var applist = pebble.GetAppbankContents().AppBank.Apps;
            Console.WriteLine("Choose an app to remove");
            AppBank.App result = SharpMenu<AppBank.App>.WriteAndPrompt(applist);
            AppbankInstallMessageEventArgs ev = pebble.RemoveApp(result);
            if (ev!=null) Console.WriteLine(ev.MsgType);
        }

        static void pebble_OnDisconnect(object sender, EventArgs e)
        {
            Console.WriteLine("Pebble disconnected.  Hit enter to exit.");
            Console.ReadLine();
            System.Environment.Exit(0);
        }

        static void pebble_MediaControlReceived(object sender, MediaControlReceivedEventArgs e)
        {
            Console.WriteLine("Received " + e.Command.ToString());
        }

        static void pebble_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            // Method for testing anything.
        }

        static void pebble_PingReceived(object sender, PingReceivedEventArgs e)
        {
            Console.WriteLine("Received PING reply: " + e.Cookie.ToString());
        }

        static void pebble_LogReceived(object sender, LogReceivedEventArgs e)
        {
            Console.WriteLine(e);
        }

        static void pingReceived(object sender, MessageReceivedEventArgs e)
        {
            Console.WriteLine("Received a ping through generic endpoint handler");
        }
    }
}
