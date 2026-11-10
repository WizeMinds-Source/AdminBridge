using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WizeMindsAdminBridge {
public static class Program {
 static readonly Encoding Utf8=new UTF8Encoding(false,true);
 static readonly byte[] Entropy=Encoding.UTF8.GetBytes("WizeMindsAdminBridge/v1");
 internal static string Json(object o) { return new JavaScriptSerializer{MaxJsonLength=1048576}.Serialize(o); }
 internal static Dictionary<string,object> Parse(string s) { return new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=16}.Deserialize<Dictionary<string,object>>(s); }
 internal static string S(Dictionary<string,object> d,string k) { object v; return d.TryGetValue(k,out v) && v is string ? (string)v : ""; }
 internal static int N(Dictionary<string,object> d,string k,int fallback) { object v; if(!d.TryGetValue(k,out v))return fallback; if(!(v is int))throw new ArgumentException(k+" must be an integer.");return (int)v; }
 internal static bool B(Dictionary<string,object> d,string k) { object v; return d.TryGetValue(k,out v) && v is bool && (bool)v; }
 internal static object Obj(params object[] pairs) { var d=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)d.Add((string)pairs[i],pairs[i+1]);return d; }
 internal static string Sid { get { return WindowsIdentity.GetCurrent().User.Value; } }
 internal static bool Admin { get { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } }
 internal static string Random(int n) { var b=new byte[n];using(var r=RandomNumberGenerator.Create())r.GetBytes(b);return Convert.ToBase64String(b); }
 internal static string Mac(string key,string data) { using(var h=new HMACSHA256(Convert.FromBase64String(key)))return Convert.ToBase64String(h.ComputeHash(Utf8.GetBytes(data))); }
 internal static bool Equal(string a,string b) { var x=Encoding.UTF8.GetBytes(a);var y=Encoding.UTF8.GetBytes(b);int z=x.Length^y.Length;for(int i=0;i<Math.Min(x.Length,y.Length);i++)z|=x[i]^y[i];return z==0; }
 internal static void Atomic(string path,string text) { string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var b=Utf8.GetBytes(text);f.Write(b,0,b.Length);f.Flush(true);}if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path); }
 internal static void NoLinks(string path) { var p=Path.GetFullPath(path);while(!String.IsNullOrEmpty(p)){if((Directory.Exists(p)||File.Exists(p)) && (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Reparse points are not allowed: "+p);p=Path.GetDirectoryName(p);} }
 internal static void Protect(string path,bool development) {
  var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
  foreach(string sid in new[]{"S-1-5-18","S-1-5-32-544"}.Concat(development?new[]{Sid}:new string[0]))
   acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
  acl.SetOwner(new SecurityIdentifier(development?Sid:"S-1-5-32-544"));Directory.SetAccessControl(path,acl);
 }
 internal static void Protected(string path) {
  NoLinks(path); FileSystemSecurity acl=Directory.Exists(path)?(FileSystemSecurity)Directory.GetAccessControl(path):File.GetAccessControl(path);
  string owner=acl.GetOwner(typeof(SecurityIdentifier)).Value;
  if(owner!="S-1-5-32-544" && owner!="S-1-5-18")throw new UnauthorizedAccessException("State/code must be owned by Administrators or SYSTEM: "+path);
  var write=FileSystemRights.Write|FileSystemRights.Delete|FileSystemRights.DeleteSubdirectoriesAndFiles|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership;
  foreach(FileSystemAccessRule a in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
   if(a.AccessControlType==AccessControlType.Allow && (a.FileSystemRights&write)!=0 && a.IdentityReference.Value!="S-1-5-32-544" && a.IdentityReference.Value!="S-1-5-18")throw new UnauthorizedAccessException("Untrusted write access: "+path);
 }
 static Dictionary<string,string> Args(string[] args) { var d=new Dictionary<string,string>();for(int i=1;i<args.Length;i++){if(!args[i].StartsWith("--"))throw new ArgumentException("Expected --option.");string k=args[i].Substring(2);d.Add(k,i+1<args.Length&&!args[i+1].StartsWith("--")?args[++i]:"true");}return d; }
 static string Required(Dictionary<string,string> a,string key) { string v;if(!a.TryGetValue(key,out v))throw new ArgumentException("Missing --"+key);return v; }
 public static int Main(string[] args) {
  Console.OutputEncoding=new UTF8Encoding(false);Console.InputEncoding=new UTF8Encoding(false);
  try {
   if(args.Length==0)throw new ArgumentException("Use init, serve, call, local-revoke, or identity. See README.txt.");
   var a=Args(args);
   switch(args[0]) {
    case "identity":Console.WriteLine(Json(Obj("user",WindowsIdentity.GetCurrent().Name,"sid",Sid,"administrator",Admin,"machine",Environment.MachineName)));break;
    case "check-protected":Protected(Required(a,"path"));Console.WriteLine("Protected permissions verified.");break;
    case "init":Init(a);break;
    case "serve":new Broker(Required(a,"state"),a.ContainsKey("development")).Run();break;
    case "call":Console.WriteLine(Call(Required(a,"credential"),a.ContainsKey("request")?File.ReadAllText(a["request"],Utf8):Required(a,"json")));break;
    case "verify-audit":VerifyAudit(Required(a,"state"));break;
    case "local-revoke":
     string state=Path.GetFullPath(Required(a,"state"));if(!a.ContainsKey("development")){if(!Admin)throw new UnauthorizedAccessException("Elevation required.");Protected(state);}
     var grant=Parse(File.ReadAllText(Path.Combine(state,"grant.json"),Utf8));
     if(B(grant,"development")!=a.ContainsKey("development"))throw new UnauthorizedAccessException("Grant mode mismatch.");
     Atomic(Path.Combine(state,"revoked"),DateTime.UtcNow.ToString("o"));
     string localRecord=Json(Obj("sequence",1,"utc",DateTime.UtcNow.ToString("o"),"event","local_revocation","project",S(grant,"id"),"previous","","data",Obj("user",Sid,"administrator",Admin)));
     Atomic(Path.Combine(state,"audit-local-"+Guid.NewGuid().ToString("N")+".jsonl"),Json(Obj("record",localRecord,"mac",Mac(S(grant,"key"),localRecord)))+"\n");
     Console.WriteLine("Grant revoked; a running broker observes this within 100 ms.");break;
    default:throw new ArgumentException("Unknown mode.");
   }
   return 0;
  } catch(Exception e) { Console.Error.WriteLine(Json(Obj("ok",false,"error",e.GetBaseException().Message,"detail",e.ToString())));return 1; }
 }
 static void VerifyAudit(string state) {
  var grant=Parse(File.ReadAllText(Path.Combine(state,"grant.json"),Utf8));int records=0,files=0;
  foreach(var file in Directory.GetFiles(state,"audit-*.jsonl")) {long seq=0;string previous="";foreach(var line in File.ReadLines(file,Utf8)) {
   var entry=Parse(line);string payload=S(entry,"record"),mac=S(entry,"mac");var record=Parse(payload);
   if(!Equal(mac,Mac(S(grant,"key"),payload))||S(record,"previous")!=previous||Convert.ToInt64(record["sequence"])!=++seq)throw new IOException("Audit verification failed: "+Path.GetFileName(file));previous=mac;records++;
  }files++;}Console.WriteLine(Json(Obj("ok",true,"files",files,"records",records)));
 }
 static void Init(Dictionary<string,string> a) {
  bool dev=a.ContainsKey("development");if(!dev&&!Admin)throw new UnauthorizedAccessException("Grant creation requires administrator elevation.");
  if(dev&&Admin)throw new UnauthorizedAccessException("Development mode must never run elevated.");
  string root=Path.GetFullPath(Required(a,"root"));NoLinks(root);if(!Directory.Exists(root))throw new DirectoryNotFoundException(root);
  string state=Path.GetFullPath(Required(a,"state"));NoLinks(state);
  if(!dev){string trusted=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"WizeMindsAdminBridge");if(!String.Equals(Path.GetDirectoryName(state),trusted,StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("Production state must be a direct child of ProgramData\\WizeMindsAdminBridge.");Protected(trusted);}
  if(Directory.Exists(state))throw new IOException("State already exists. Create a new grant; existing grants are never silently renewed.");
  string credential=Path.GetFullPath(Required(a,"credential"));NoLinks(credential);if(File.Exists(credential))throw new IOException("Credential already exists.");
  if(!Directory.Exists(Path.GetDirectoryName(credential)))throw new DirectoryNotFoundException("Create the credential parent directory first.");
  Directory.CreateDirectory(state);Protect(state,dev);
  string id=Guid.NewGuid().ToString("N"),key=Random(32),pipe="WizeMindsAdminBridge-"+id;
  var grant=Obj("version",1,"id",id,"root",root,"sid",Sid,"key",key,"pipe",pipe,"development",dev,"created",DateTime.UtcNow.ToString("o"));
  Atomic(Path.Combine(state,"grant.json"),Json(grant));
  var client=Obj("version",1,"id",id,"root",root,"pipe",pipe,"key",key,"development",dev);
  byte[] encrypted=ProtectedData.Protect(Utf8.GetBytes(Json(client)),Entropy,DataProtectionScope.CurrentUser);
  using(var f=new FileStream(credential,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(encrypted,0,encrypted.Length);f.Flush(true);}
  var ca=new FileSecurity();ca.SetAccessRuleProtection(true,false);ca.SetOwner(new SecurityIdentifier(Sid));ca.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(Sid),FileSystemRights.FullControl,AccessControlType.Allow));File.SetAccessControl(credential,ca);
  Console.WriteLine(Json(Obj("id",id,"root",root,"state",state,"credential",credential,"development",dev)));
 }
 static async Task<byte[]> ReadExact(Stream s,int n,CancellationToken token) { byte[] b=new byte[n];int offset=0;while(offset<n){int got=await s.ReadAsync(b,offset,n-offset,token).ConfigureAwait(false);if(got==0)throw new EndOfStreamException();offset+=got;}return b; }
 internal static async Task<string> Read(Stream s,CancellationToken token) { byte[] head=await ReadExact(s,4,token).ConfigureAwait(false);int n=BitConverter.ToInt32(head,0);if(n<1||n>262144)throw new IOException("Invalid frame length.");return Utf8.GetString(await ReadExact(s,n,token).ConfigureAwait(false)); }
 internal static async Task Write(Stream s,string text,CancellationToken token) { byte[] data=Utf8.GetBytes(text);if(data.Length>262144)throw new IOException("Response too large.");byte[] head=BitConverter.GetBytes(data.Length);await s.WriteAsync(head,0,4,token).ConfigureAwait(false);await s.WriteAsync(data,0,data.Length,token).ConfigureAwait(false);await s.FlushAsync(token).ConfigureAwait(false); }
 public static string Call(string credential,string request) {
  var c=Parse(Utf8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(credential),Entropy,DataProtectionScope.CurrentUser)));
  var q=Parse(request);q["project"]=S(c,"id");q["requestId"]=Guid.NewGuid().ToString("N");string payload=Json(q);
  using(var pipe=Native.Client(S(c,"pipe")))using(var timeout=new CancellationTokenSource(10000))using(timeout.Token.Register(()=>pipe.Dispose())) {
   var t=timeout.Token;string cn=Random(32),id=S(c,"id"),key=S(c,"key");
   Write(pipe,Json(Obj("project",id,"nonce",cn)),t).GetAwaiter().GetResult();
   var hello=Parse(Read(pipe,t).GetAwaiter().GetResult());string sn=S(hello,"nonce"),context=id+"\n"+cn+"\n"+sn;
   if(!Equal(S(hello,"mac"),Mac(key,"server\n"+context)))throw new UnauthorizedAccessException("Broker authentication failed.");
   Write(pipe,Json(Obj("payload",payload,"mac",Mac(key,"request\n"+context+"\n"+payload))),t).GetAwaiter().GetResult();
   var response=Parse(Read(pipe,t).GetAwaiter().GetResult());string body=S(response,"payload");
   if(!Equal(S(response,"mac"),Mac(key,"response\n"+context+"\n"+body)))throw new UnauthorizedAccessException("Response authentication failed.");return body;
  }
 }
 internal sealed class Job {
  internal string Id,Shell,State,Directory,Hash;internal DateTime Started,Finished;internal int Timeout;internal uint? Exit;internal Native.Tree Tree;
  internal object Summary() { return Obj("id",Id,"shell",Shell,"state",State,"started",Started.ToString("o"),"finished",Finished==default(DateTime)?null:Finished.ToString("o"),"exitCode",Exit,"timeoutSeconds",Timeout,"commandSha256",Hash); }
 }
 internal sealed class Broker {
  readonly object gate=new object();readonly string state,root,id,key,sid,pipeName;readonly bool dev;readonly Dictionary<string,Job> jobs=new Dictionary<string,Job>();
  volatile bool stopping; bool revoked;FileStream exclusive,audit;Timer timer;int clients;long sequence;string previous="";
  internal Broker(string path,bool development) {
   state=Path.GetFullPath(path);NoLinks(state);dev=development;
   if(dev&&Admin)throw new UnauthorizedAccessException("Development mode must never run elevated.");
   if(!dev){if(!Admin)throw new UnauthorizedAccessException("Administrator elevation is required.");string trusted=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"WizeMindsAdminBridge");if(!String.Equals(Path.GetDirectoryName(state),trusted,StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("Untrusted state parent.");Protected(trusted);Protected(state);Protected(Path.Combine(state,"grant.json"));Protected(Process.GetCurrentProcess().MainModule.FileName);Protected(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));}
   var g=Parse(File.ReadAllText(Path.Combine(state,"grant.json"),Utf8));
   if(B(g,"development")!=dev)throw new UnauthorizedAccessException("Development and production grants cannot be mixed.");
   id=S(g,"id");key=S(g,"key");sid=S(g,"sid");pipeName=S(g,"pipe");root=S(g,"root");NoLinks(root);
   revoked=File.Exists(Path.Combine(state,"revoked"));
  }
  void Log(string action,object data) {
   var record=Obj("sequence",++sequence,"utc",DateTime.UtcNow.ToString("o"),"event",action,"project",id,"previous",previous,"data",data);
   string payload=Json(record);previous=Mac(key,payload);byte[] bytes=Utf8.GetBytes(Json(Obj("record",payload,"mac",previous))+"\n");audit.Write(bytes,0,bytes.Length);audit.Flush(true);
  }
  void Save(Job j){Atomic(Path.Combine(j.Directory,"job.json"),Json(j.Summary()));}
  void Finish(Job j,string reason) {
   if(j.Tree==null)return;
   if(reason!="Completed")j.Tree.Stop();
   j.Exit=j.Tree.ExitCode;j.State=reason;j.Finished=DateTime.UtcNow;j.Tree.Dispose();j.Tree=null;
   if(reason=="OutputLimit"){long remaining=16*1024*1024;foreach(string name in new[]{"stdout.bin","stderr.bin"})using(var f=new FileStream(Path.Combine(j.Directory,name),FileMode.Open,FileAccess.Write,FileShare.ReadWrite)){if(f.Length>remaining)f.SetLength(remaining);remaining-=f.Length;}}
   Save(j);Log("job_finished",j.Summary());
  }
  void Revoke(string source) {
   Atomic(Path.Combine(state,"revoked"),DateTime.UtcNow.ToString("o"));revoked=true;
   try {foreach(var j in jobs.Values.Where(x=>x.Tree!=null).ToArray())Finish(j,"Revoked");Log("revoked",Obj("source",source));}
   catch {stopping=true;foreach(var j in jobs.Values)if(j.Tree!=null){j.Tree.Dispose();j.Tree=null;}throw;}
  }
  void Tick(object unused) {
   lock(gate) {if(stopping)return;try {
    if(!revoked&&File.Exists(Path.Combine(state,"revoked")))Revoke("local_control");
    foreach(var j in jobs.Values.Where(x=>x.Tree!=null).ToArray()) {
     if(new FileInfo(Path.Combine(j.Directory,"stdout.bin")).Length+new FileInfo(Path.Combine(j.Directory,"stderr.bin")).Length>16*1024*1024)Finish(j,"OutputLimit");
     else if(j.Tree.Active==0)Finish(j,"Completed");
     else if((DateTime.UtcNow-j.Started).TotalSeconds>=j.Timeout)Finish(j,"TimedOut");
    }
   } catch { stopping=true;foreach(var j in jobs.Values)if(j.Tree!=null){j.Tree.Dispose();j.Tree=null;} } }
  }
  internal void Run() {
   exclusive=new FileStream(Path.Combine(state,"broker.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
   string logPath=Path.Combine(state,"audit-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".jsonl");
   audit=new FileStream(logPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
   NamedPipeServerStream listener=null;
   try {
    string jobRoot=Path.Combine(state,"jobs");Directory.CreateDirectory(jobRoot);
    foreach(string dir in Directory.GetDirectories(jobRoot)) {
     NoLinks(dir);var f=Path.Combine(dir,"job.json");if(!File.Exists(f))continue;var m=Parse(File.ReadAllText(f,Utf8));
     var j=new Job{Id=S(m,"id"),Shell=S(m,"shell"),State=S(m,"state"),Directory=dir,Hash=S(m,"commandSha256"),Started=DateTime.Parse(S(m,"started")).ToUniversalTime(),Timeout=N(m,"timeoutSeconds",3600)};
     if(j.State=="Running"){j.State="Interrupted";j.Finished=DateTime.UtcNow;Save(j);}else {if(S(m,"finished")!="")j.Finished=DateTime.Parse(S(m,"finished")).ToUniversalTime();if(m.ContainsKey("exitCode")&&m["exitCode"]!=null)j.Exit=Convert.ToUInt32(m["exitCode"]);}
     jobs.Add(j.Id,j);
    }
    Atomic(Path.Combine(state,"broker-process.json"),Json(Obj("pid",Process.GetCurrentProcess().Id,"startTicks",Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString(),"executable",Process.GetCurrentProcess().MainModule.FileName)));
    Log("broker_started",Obj("administrator",Admin,"development",dev,"root",root,"sid",sid,"pid",Process.GetCurrentProcess().Id));
    listener=Native.Server(pipeName,sid,true,dev);timer=new Timer(Tick,null,100,100);
    Console.WriteLine(Json(Obj("ready",true,"pipe",pipeName,"administrator",Admin,"project",id)));
    while(!stopping) {
     var wait=listener.BeginWaitForConnection(null,null);
     while(!wait.AsyncWaitHandle.WaitOne(100)&&!stopping){}
     if(stopping)break;listener.EndWaitForConnection(wait);wait.AsyncWaitHandle.Close();
     var accepted=listener;listener=Native.Server(pipeName,sid,false,dev);
     if(Interlocked.Increment(ref clients)>8){Interlocked.Decrement(ref clients);accepted.Dispose();continue;}
     Task.Run(()=>Handle(accepted));
    }
   } finally {
    stopping=true;if(listener!=null)listener.Dispose();if(timer!=null)timer.Dispose();
    lock(gate){foreach(var j in jobs.Values.Where(x=>x.Tree!=null).ToArray()){try{Finish(j,"Shutdown");}catch{if(j.Tree!=null){j.Tree.Dispose();j.Tree=null;}}}try{Log("broker_stopped",Obj());}catch{} }
    var end=DateTime.UtcNow.AddSeconds(9);while(clients>0&&DateTime.UtcNow<end)Thread.Sleep(50);
    audit.Dispose();exclusive.Dispose();
   }
  }
  async Task Handle(NamedPipeServerStream pipe) {
   using(pipe)using(var timeout=new CancellationTokenSource(8000))using(timeout.Token.Register(()=>pipe.Dispose())) {
    try {
     var t=timeout.Token;var hello=Parse(await Read(pipe,t).ConfigureAwait(false));
     string peer="";pipe.RunAsClient(()=>peer=WindowsIdentity.GetCurrent().User.Value);
     if(peer!=sid||S(hello,"project")!=id||S(hello,"nonce").Length!=44)throw new UnauthorizedAccessException("Client identity rejected.");
     string sn=Random(32),context=id+"\n"+S(hello,"nonce")+"\n"+sn;
     await Write(pipe,Json(Obj("nonce",sn,"mac",Mac(key,"server\n"+context))),t).ConfigureAwait(false);
     var envelope=Parse(await Read(pipe,t).ConfigureAwait(false));string payload=S(envelope,"payload");
     if(!Equal(S(envelope,"mac"),Mac(key,"request\n"+context+"\n"+payload)))throw new UnauthorizedAccessException("Invalid request authentication.");
     var request=Parse(payload);string response;
     lock(gate) {
      try {if(stopping)throw new InvalidOperationException("Broker is stopping.");if(S(request,"project")!=id)throw new UnauthorizedAccessException("Wrong project.");
       Log("request",Obj("requestId",S(request,"requestId"),"action",S(request,"action"),"user",peer));
       response=Json(Obj("ok",true,"result",Dispatch(request)));
      } catch(Exception e) {Log("request_error",Obj("requestId",S(request,"requestId"),"error",e.Message));response=Json(Obj("ok",false,"error",e.Message));}
     }
     await Write(pipe,Json(Obj("payload",response,"mac",Mac(key,"response\n"+context+"\n"+response))),t).ConfigureAwait(false);
    } catch(Exception e) {lock(gate){try{Log("connection_rejected",Obj("reason",e.GetType().Name));}catch{stopping=true;}}}
    finally {Interlocked.Decrement(ref clients);}
   }
  }
  object Dispatch(Dictionary<string,object> q) {
   string action=S(q,"action");
   switch(action) {
    case "status":return Obj("project",id,"root",root,"administrator",Admin,"development",dev,"authorized",!revoked&&!File.Exists(Path.Combine(state,"revoked")),"active",jobs.Values.Count(x=>x.Tree!=null),"retained",jobs.Count);
    case "list":return jobs.Values.Select(x=>x.Summary()).ToArray();
    case "submit":return Submit(q);
    case "read":
     var j=Find(q);string stream=S(q,"stream");if(stream!="stdout"&&stream!="stderr")throw new ArgumentException("stream must be stdout or stderr.");
     int offset=N(q,"offset",0),count=N(q,"count",32768);if(offset<0||count<1||count>32768)throw new ArgumentException("Invalid read range.");
     using(var f=new FileStream(Path.Combine(j.Directory,stream+".bin"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) {if(offset>f.Length)throw new ArgumentException("Offset past output end.");f.Position=offset;byte[] data=new byte[count];int n=f.Read(data,0,count);return Obj("job",j.Summary(),"offset",offset,"nextOffset",offset+n,"availableBytes",f.Length,"base64",Convert.ToBase64String(data,0,n),"text",Encoding.UTF8.GetString(data,0,n),"encoding","UTF-8; base64 is authoritative");}
    case "stop":var stop=Find(q);if(stop.Tree!=null)Finish(stop,"Stopped");return stop.Summary();
    case "revoke":Revoke("authenticated_client");return Obj("revoked",true);
    case "shutdown":foreach(var job in jobs.Values.Where(x=>x.Tree!=null).ToArray())Finish(job,"Shutdown");stopping=true;return Obj("shutdown",true);
    default:throw new ArgumentException("Unknown action.");
   }
  }
  Job Find(Dictionary<string,object> q) {Job j;if(!jobs.TryGetValue(S(q,"id"),out j))throw new ArgumentException("Unknown job for this project.");return j;}
  object Submit(Dictionary<string,object> q) {
   if(revoked||File.Exists(Path.Combine(state,"revoked")))throw new UnauthorizedAccessException("Project authorization revoked.");
   if(jobs.Count>=256)throw new InvalidOperationException("256 retained jobs reached. Archive state and create a new grant.");
   if(jobs.Values.Count(x=>x.Tree!=null)>=4)throw new InvalidOperationException("Four running jobs allowed per project.");
   string shell=S(q,"shell"),command=S(q,"command");if(shell!="powershell"&&shell!="cmd")throw new ArgumentException("shell must be powershell or cmd.");
   if(command.Length<1||command.Length>65536||command.IndexOf('\0')>=0)throw new ArgumentException("Command must contain 1–65536 characters without NUL.");
   int timeout=N(q,"timeoutSeconds",3600);if(timeout<1||timeout>86400)throw new ArgumentException("Timeout must be 1–86400 seconds.");NoLinks(root);
   string jobId=Guid.NewGuid().ToString("N"),dir=Path.Combine(state,"jobs",jobId);Directory.CreateDirectory(dir);
   string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(command))).Replace("-","").ToLowerInvariant();
   string script=Path.Combine(dir,shell=="powershell"?"command.ps1":"command.cmd");
   // Shell source stays in administrator-protected state; quoting cannot inject into the launcher.
   File.WriteAllText(script,command,new UTF8Encoding(shell=="powershell"));
   string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows),exe,args;
   if(shell=="powershell") {exe=Path.Combine(windows,@"System32\WindowsPowerShell\v1.0\powershell.exe");string wrapper="$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); & '"+script.Replace("'","''")+"'; $commandSucceeded=$?; if ($null -ne $LASTEXITCODE) { exit $LASTEXITCODE }; if (-not $commandSucceeded) { exit 1 }";args="-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(wrapper));}
   else {exe=Path.Combine(windows,"System32\\cmd.exe");args="/d /s /c \"chcp 65001 >nul & \""+script+"\"\"";}
   var j=new Job{Id=jobId,Shell=shell,State="Running",Directory=dir,Hash=hash,Started=DateTime.UtcNow,Timeout=timeout};
   Save(j);Log("job_starting",j.Summary());
   try {j.Tree=new Native.Tree(exe,args,root,Path.Combine(dir,"stdout.bin"),Path.Combine(dir,"stderr.bin"));jobs.Add(j.Id,j);Log("job_started",Obj("id",j.Id,"pid",j.Tree.Pid));return j.Summary();}
   catch {if(j.Tree!=null)j.Tree.Dispose();j.Tree=null;j.State="StartFailed";j.Finished=DateTime.UtcNow;Save(j);throw;}
  }
 }
}
}
