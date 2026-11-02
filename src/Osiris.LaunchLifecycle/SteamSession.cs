using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Playnite.SDK.Models;
using Playnite.SDK.Events;

namespace Osiris.LaunchLifecycle
{
    public static class SteamSession
    {
        private static readonly ConditionalWeakTable<object,State> states=new ConditionalWeakTable<object,State>();
        private static object Get(object value,string name)=>value?.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(value,null);
        internal static bool IsSteam(GameAction action)
        {
            if(action==null)return false;
            var path=action.Path??"";
            return path.StartsWith("steam://run/",StringComparison.OrdinalIgnoreCase) || path.StartsWith("steam://rungameid/",StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(Path.GetFileName(path),"steam.exe",StringComparison.OrdinalIgnoreCase) && (action.Arguments??"").IndexOf("-applaunch ",StringComparison.OrdinalIgnoreCase)>=0);
        }
        public static void NormalizeAction(GameAction action)
        {
            if(action==null || ((int)action.TrackingMode!=6 && (int)action.TrackingMode!=7))return;
            var target=Path.GetFullPath(Environment.ExpandEnvironmentVariables(action.TrackingPath??""));
            if(!Path.IsPathRooted(action.TrackingPath??"") || !target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) || !File.Exists(target))throw new FileNotFoundException("The tracking executable does not exist. Choose the game's executable in Actions.",target);
            // Only the expanded launch clone is normalized. The saved action keeps
            // its exact exe and independent time preference (6=on, 7=off).
            action.TrackingPath=Path.GetDirectoryName(target);action.TrackingMode=TrackingMode.Directory;
        }
        private static State Resolve(object controller)
        {
            try
            {
                var args=Get(controller,"StartingArgs") as OnGameStartingEventArgs;
                var game=Get(controller,"Game") as Game;
                var action=args?.SourceAction;
                if(action==null || ((int)action.TrackingMode!=6 && (int)action.TrackingMode!=7))return null;
                var target=Environment.ExpandEnvironmentVariables(action.TrackingPath??"");
                target=target.Replace("{InstallDir}",game?.InstallDirectory??"");
                if(!Path.IsPathRooted(target) || !target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))return null;
                target=Path.GetFullPath(target);
                return states.GetValue(controller,_=>new State(target,Probe,()=>DateTime.UtcNow));
            }catch{return null;}
        }
        public static Func<bool> WrapTracking(object controller,Func<bool> original)
        {var state=Resolve(controller);return state==null?original:new Func<bool>(state.Running);}
        public static Func<int> WrapStartup(object controller,Func<int> original)
        {var state=Resolve(controller);return state==null?original:new Func<int>(state.Startup);}
        internal sealed class State
        {
            private readonly string folder;private readonly Func<string,int> probe;private readonly Func<DateTime> now;
            private readonly DateTime deadline;private DateTime lastSeen;private bool seen;
            internal State(string folder,Func<string,int> probe,Func<DateTime> now){this.folder=folder;this.probe=probe;this.now=now;deadline=now().AddMinutes(3);}
            internal int Startup(){var id=probe(folder);if(id>0){seen=true;lastSeen=now();return id;}if(now()>deadline)throw new TimeoutException("The tracking executable did not start within three minutes.");return 0;}
            internal bool Running(){var id=probe(folder);if(id>0){seen=true;lastSeen=now();return true;}return seen?now()-lastSeen<TimeSpan.FromSeconds(2):now()<deadline;}
        }
        internal static bool Matches(string folder,string executable)
        {
            if(string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(executable))return false;
            return string.Equals(Path.GetFullPath(folder),Path.GetFullPath(executable),StringComparison.OrdinalIgnoreCase);
        }
        [DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr OpenProcess(uint access,bool inherit,int id);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder name,ref uint length);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        private static int Probe(string folder)
        {
            foreach(var process in Process.GetProcesses())using(process)
            {
                var handle=OpenProcess(0x1000,false,process.Id);if(handle==IntPtr.Zero)continue;
                try{var text=new StringBuilder(32768);uint size=(uint)text.Capacity;if(QueryFullProcessImageName(handle,0,text,ref size) && Matches(folder,text.ToString()))return process.Id;}finally{CloseHandle(handle);}
            }
            return 0;
        }
    }
}
