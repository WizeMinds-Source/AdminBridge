using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Collections.Generic;
namespace WizeMindsAdminBridge {
internal static class ProbeTests {
 static Dictionary<string,object> credential;static int passed;
 static void Assert(bool ok,string name){if(!ok)throw new Exception("FAILED: "+name);Console.WriteLine("PASS "+name);passed++;}
 static string C(string k){return Program.S(credential,k);}
 static string Read(Stream p,CancellationToken t){return Program.Read(p,t).GetAwaiter().GetResult();}
 static void Write(Stream p,string s,CancellationToken t){Program.Write(p,s,t).GetAwaiter().GetResult();}
 static bool Rejected(Action action){try{action();return false;}catch(IOException){return true;}catch(ObjectDisposedException){return true;}catch(OperationCanceledException){return true;}}
 static string Exchange(string project,string key,bool replay,out string envelope) {
  using(var p=Native.Client(C("pipe")))using(var ct=new CancellationTokenSource(10000))using(ct.Token.Register(()=>p.Dispose())){
   string cn=Program.Random(32);Write(p,Program.Json(Program.Obj("project",project,"nonce",cn)),ct.Token);
   var hello=Program.Parse(Read(p,ct.Token));string context=project+"\n"+cn+"\n"+Program.S(hello,"nonce");
   string payload=Program.Json(Program.Obj("project",project,"action","status"));
   envelope=Program.Json(Program.Obj("payload",payload,"mac",Program.Mac(key,"request\n"+context+"\n"+payload)));
   Write(p,envelope,ct.Token);return Read(p,ct.Token);
  }
 }
 public static int Main(string[] args){try {
  credential=Program.Parse(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(args[0]),Encoding.UTF8.GetBytes("WizeMindsAdminBridge/v1"),DataProtectionScope.CurrentUser)));
  string ignored="";
  Assert(Rejected(()=>Exchange(C("id"),Program.Random(32),false,out ignored)),"Wrong HMAC key rejected");
  Assert(Rejected(()=>Exchange(Guid.NewGuid().ToString("N"),C("key"),false,out ignored)),"Wrong project rejected before authentication");
  string previous;Exchange(C("id"),C("key"),false,out previous);
  Assert(Rejected(()=>{using(var p=Native.Client(C("pipe")))using(var ct=new CancellationTokenSource(10000))using(ct.Token.Register(()=>p.Dispose())){Write(p,Program.Json(Program.Obj("project",C("id"),"nonce",Program.Random(32))),ct.Token);Read(p,ct.Token);Write(p,previous,ct.Token);Read(p,ct.Token);}}),"Captured authenticated request cannot be replayed");
  Assert(Rejected(()=>{using(var p=Native.Client(C("pipe")))using(var ct=new CancellationTokenSource(10000))using(ct.Token.Register(()=>p.Dispose())){byte[] header=BitConverter.GetBytes(int.MaxValue);p.Write(header,0,4);Read(p,ct.Token);}}),"Oversized frame rejected before allocation");
  Assert(Rejected(()=>{using(var p=Native.Client(C("pipe")))using(var ct=new CancellationTokenSource(10000))using(ct.Token.Register(()=>p.Dispose())){Write(p,"not-json",ct.Token);Read(p,ct.Token);}}),"Malformed JSON rejected");
  using(var idle=Native.Client(C("pipe"))){
   string status=Program.Call(args[0],"{\"action\":\"status\"}");Assert(Program.B(Program.Parse(status),"ok"),"Idle client does not block legitimate controls");
   Thread.Sleep(8500);Assert(Rejected(()=>{byte[] b=BitConverter.GetBytes(1);idle.Write(b,0,4);idle.WriteByte(32);using(var ct=new CancellationTokenSource(1000))Read(idle,ct.Token);}),"Idle client disconnected at deadline");
  }
  string good=Program.Call(args[0],"{\"action\":\"status\"}");Assert(Program.B(Program.Parse(good),"ok"),"Broker remains healthy after authentication attacks");
  Console.WriteLine("All "+passed+" authentication checks passed.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}}
}
}
