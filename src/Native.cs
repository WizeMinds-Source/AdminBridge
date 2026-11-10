using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WizeMindsAdminBridge {
internal static class Native {
 [StructLayout(LayoutKind.Sequential)] internal struct SA { public int size; public IntPtr descriptor; public int inherit; }
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] internal struct SI { public int cb; public string reserved,desktop,title; public int x,y,cx,cy,xChars,yChars,fill,flags; public short show,reserved2; public IntPtr reservedPtr,input,output,error; }
 [StructLayout(LayoutKind.Sequential)] internal struct SIX { public SI si; public IntPtr attributes; }
 [StructLayout(LayoutKind.Sequential)] internal struct PI { public IntPtr process,thread; public int pid,tid; }
 [StructLayout(LayoutKind.Sequential)] internal struct BASIC { public long processTime,jobTime; public uint flags; public UIntPtr min,max; public uint active; public UIntPtr affinity; public uint priority,scheduling; }
 [StructLayout(LayoutKind.Sequential)] internal struct IOCOUNT { public ulong a,b,c,d,e,f; }
 [StructLayout(LayoutKind.Sequential)] internal struct LIMIT { public BASIC basic; public IOCOUNT io; public UIntPtr processMemory,jobMemory,peakProcess,peakJob; }
 [StructLayout(LayoutKind.Sequential)] internal struct ACCOUNT { public long a,b,c,d; public uint faults,total,active,terminated; }
 [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern SafePipeHandle CreateNamedPipe(string name,uint open,uint mode,uint instances,uint output,uint input,uint timeout,ref SA sa);
 [DllImport("advapi32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string s,uint revision,out IntPtr descriptor,out uint size);
 [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);
 [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern SafePipeHandle CreateFile(string name,uint access,uint share,IntPtr sa,uint creation,uint flags,IntPtr template);
 [DllImport("kernel32.dll", EntryPoint="CreateFileW", SetLastError=true, CharSet=CharSet.Unicode)] static extern SafeFileHandle OpenInput(string name,uint access,uint share,IntPtr sa,uint creation,uint flags,IntPtr template);
 [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern bool WaitNamedPipe(string name,uint timeout);
 [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr sa,string name);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool SetInformationJobObject(IntPtr job,int cls,ref LIMIT info,int size);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool QueryInformationJobObject(IntPtr job,int cls,out ACCOUNT info,int size,IntPtr length);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
 [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool TerminateJobObject(IntPtr job,uint code);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool TerminateProcess(IntPtr process,uint code);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetExitCodeProcess(IntPtr process,out uint code);
 [DllImport("kernel32.dll", SetLastError=true)] static extern uint ResumeThread(IntPtr thread);
 [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern bool CreateProcess(string app,StringBuilder command,IntPtr pa,IntPtr ta,bool inherit,uint flags,IntPtr environment,string cwd,ref SIX si,out PI pi);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,int flags,ref IntPtr size);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,IntPtr attribute,IntPtr value,IntPtr size,IntPtr previous,IntPtr returned);
 [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr list);
 [DllImport("kernel32.dll", SetLastError=true)] static extern bool SetHandleInformation(IntPtr handle,uint mask,uint flags);
 internal static void Check(bool ok) { if(!ok) throw new Win32Exception(Marshal.GetLastWin32Error()); }
 internal static NamedPipeServerStream Server(string name,string sid,bool first,bool development) {
  IntPtr descriptor; uint size;
  Check(ConvertStringSecurityDescriptorToSecurityDescriptor("D:P(D;;GA;;;NU)(A;;GA;;;SY)(A;;GA;;;BA)(A;;"+(development?"GA":"0x12019b")+";;;"+sid+")",1,out descriptor,out size));
  try { var sa=new SA{size=Marshal.SizeOf(typeof(SA)),descriptor=descriptor};
   var handle=CreateNamedPipe(@"\\.\pipe\"+name,3|0x40000000u|(first?0x80000u:0),8,16,65536,65536,0,ref sa);
   if(handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
   return new NamedPipeServerStream(PipeDirection.InOut,true,false,handle);
  } finally { LocalFree(descriptor); }
 }
 internal static NamedPipeClientStream Client(string name) {
  string path=@"\\.\pipe\"+name;
  var until=DateTime.UtcNow.AddSeconds(8);
  do { var h=CreateFile(path,0x12019b,0,IntPtr.Zero,3,0x40000000|0x100000|0x10000,IntPtr.Zero);
   if(!h.IsInvalid) return new NamedPipeClientStream(PipeDirection.InOut,true,true,h);
   int error=Marshal.GetLastWin32Error(); h.Dispose();
   if(error!=2 && error!=231) throw new Win32Exception(error);
   WaitNamedPipe(path,100); System.Threading.Thread.Sleep(30);
  } while(DateTime.UtcNow<until);
  throw new TimeoutException("Broker is not running or is busy.");
 }
 internal sealed class Tree : IDisposable {
  internal IntPtr job,process;
  internal int Pid;
  internal Tree(string exe,string args,string cwd,string stdout,string stderr) {
   job=CreateJobObject(IntPtr.Zero,null); Check(job!=IntPtr.Zero);
   var limit=new LIMIT(); limit.basic.flags=0x2000|0x400|8; limit.basic.active=64;
   Check(SetInformationJobObject(job,9,ref limit,Marshal.SizeOf(typeof(LIMIT))));
   PI pi=new PI(); IntPtr attrs=IntPtr.Zero, handles=IntPtr.Zero;
   try {
    using(var output=new FileStream(stdout,FileMode.CreateNew,FileAccess.Write,FileShare.ReadWrite))
    using(var error=new FileStream(stderr,FileMode.CreateNew,FileAccess.Write,FileShare.ReadWrite))
    using(var input=OpenInput("NUL",0x80000000,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
     Check(!input.IsInvalid);
     IntPtr[] hs={input.DangerousGetHandle(),output.SafeFileHandle.DangerousGetHandle(),error.SafeFileHandle.DangerousGetHandle()};
     foreach(var h in hs) Check(SetHandleInformation(h,1,1));
     IntPtr size=IntPtr.Zero; InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size);
     attrs=Marshal.AllocHGlobal(size); Check(InitializeProcThreadAttributeList(attrs,1,0,ref size));
     handles=Marshal.AllocHGlobal(IntPtr.Size*3); Marshal.Copy(hs,0,handles,3);
     Check(UpdateProcThreadAttribute(attrs,0,new IntPtr(0x20002),handles,new IntPtr(IntPtr.Size*3),IntPtr.Zero,IntPtr.Zero));
     var si=new SIX(); si.si.cb=Marshal.SizeOf(typeof(SIX)); si.si.flags=0x100; si.si.input=hs[0]; si.si.output=hs[1]; si.si.error=hs[2]; si.attributes=attrs;
     Check(CreateProcess(exe,new StringBuilder("\""+exe+"\" "+args),IntPtr.Zero,IntPtr.Zero,true,0x08000000|0x80000|4,IntPtr.Zero,cwd,ref si,out pi));
     process=pi.process; Pid=pi.pid;
     Check(AssignProcessToJobObject(job,process));
     if(ResumeThread(pi.thread)==uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
   } catch { if(pi.process!=IntPtr.Zero) TerminateProcess(pi.process,125); Dispose(); throw; }
   finally { if(pi.thread!=IntPtr.Zero) CloseHandle(pi.thread); if(attrs!=IntPtr.Zero){DeleteProcThreadAttributeList(attrs);Marshal.FreeHGlobal(attrs);} if(handles!=IntPtr.Zero) Marshal.FreeHGlobal(handles); }
  }
  internal uint Active { get { ACCOUNT a; Check(QueryInformationJobObject(job,1,out a,Marshal.SizeOf(typeof(ACCOUNT)),IntPtr.Zero)); return a.active; } }
  internal uint ExitCode { get { uint code; Check(GetExitCodeProcess(process,out code)); return code; } }
  internal void Stop() { if(job==IntPtr.Zero)return;Check(TerminateJobObject(job,1223));var end=DateTime.UtcNow.AddSeconds(3);while(Active!=0&&DateTime.UtcNow<end)System.Threading.Thread.Sleep(10);if(Active!=0)throw new IOException("Windows has not confirmed process-tree termination."); }
  public void Dispose() { if(job!=IntPtr.Zero){CloseHandle(job);job=IntPtr.Zero;} if(process!=IntPtr.Zero){CloseHandle(process);process=IntPtr.Zero;} }
 }
}
}
