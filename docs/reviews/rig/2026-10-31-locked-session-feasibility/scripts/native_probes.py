from pathlib import Path
import ctypes as C, ctypes.wintypes as W, json, struct, time, sys
from PIL import Image, ImageStat
stage=Path(__file__).parent
sys.path.insert(0,str(stage)); import rig
u=C.WinDLL('user32',use_last_error=True); g=C.WinDLL('gdi32',use_last_error=True); k=C.WinDLL('kernel32',use_last_error=True)
run=json.loads(Path('C:/BigAmbitions-AgentTests/run.json').read_text(encoding='utf-8-sig')); pid=run['pid']
u.GetForegroundWindow.restype=W.HWND; u.GetWindowDC.argtypes=[W.HWND];u.GetWindowDC.restype=W.HDC
u.GetWindowThreadProcessId.argtypes=[W.HWND,C.POINTER(W.DWORD)]
u.SetForegroundWindow.argtypes=[W.HWND];u.PostMessageW.argtypes=[W.HWND,W.UINT,W.WPARAM,W.LPARAM]
u.GetWindowRect.argtypes=[W.HWND,C.POINTER(W.RECT)]
def windowpid(hwnd):
    v=W.DWORD();u.GetWindowThreadProcessId(hwnd,C.byref(v));return v.value
wins=[]
@C.WINFUNCTYPE(W.BOOL,W.HWND,W.LPARAM)
def enum(hwnd,unused):
    if windowpid(hwnd)==pid: wins.append(hwnd)
    return True
u.EnumWindows(enum,0)
owned=[]
u.GetWindowTextW.argtypes=[W.HWND,W.LPWSTR,C.c_int]
for wh in wins:
    b=C.create_unicode_buffer(512);u.GetWindowTextW(wh,b,512);owned.append({'hwnd':int(wh),'title':b.value,'visible':bool(u.IsWindowVisible(wh))})
rig.record('owned-windows',owned)
wins=[v['hwnd'] for v in owned if v['title']=='Big Ambitions Agent Tests']
assert len(wins)==1,owned
hwnd=wins[0];rect=W.RECT();u.GetWindowRect(hwnd,C.byref(rect))
def desktop():
    u.OpenInputDesktop.argtypes=[W.DWORD,W.BOOL,W.DWORD];u.OpenInputDesktop.restype=W.HANDLE
    h=u.OpenInputDesktop(0,False,1);err=C.get_last_error()
    result={'openInputDesktopSucceeded':bool(h),'error':0 if h else err}
    if h:
        b=C.create_unicode_buffer(256);n=W.DWORD();u.GetUserObjectInformationW.argtypes=[W.HANDLE,C.c_int,C.c_void_p,W.DWORD,C.POINTER(W.DWORD)]
        result['nameRead']=bool(u.GetUserObjectInformationW(h,2,b,C.sizeof(b),C.byref(n)));result['name']=b.value;u.CloseDesktop.argtypes=[W.HANDLE];u.CloseDesktop(h)
    wts=C.WinDLL('wtsapi32',use_last_error=True);p=C.c_void_p();n=W.DWORD();wts.WTSQuerySessionInformationW.argtypes=[W.HANDLE,W.DWORD,C.c_int,C.POINTER(C.c_void_p),C.POINTER(W.DWORD)]
    ok=wts.WTSQuerySessionInformationW(None,11,25,C.byref(p),C.byref(n));result['wtsInfoExRead']=bool(ok)
    if ok:
        data=C.string_at(p,n.value);level=struct.unpack_from('<I',data)[0];sid,state,flags=struct.unpack_from('<Iii',data,8);result.update(level=level,sessionId=sid,sessionState=state,sessionFlags=flags,locked=flags==0);wts.WTSFreeMemory.argtypes=[C.c_void_p];wts.WTSFreeMemory(p)
    return result
class Power(C.Structure):_fields_=[('version',W.DWORD),('controlMask',W.DWORD),('stateMask',W.DWORD)]
k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE;k.GetProcessInformation.argtypes=[W.HANDLE,C.c_int,C.c_void_p,W.DWORD];k.CloseHandle.argtypes=[W.HANDLE]
h=k.OpenProcess(0x1000,False,pid);power=Power(1,0,0);ok=k.GetProcessInformation(h,4,C.byref(power),C.sizeof(power));pe=C.get_last_error();k.CloseHandle(h)
info={'pid':pid,'hwnd':int(hwnd),'rect':[rect.left,rect.top,rect.right,rect.bottom],'desktop':desktop(),'powerThrottling':{'read':bool(ok),'error':0 if ok else pe,'controlMask':power.controlMask,'stateMask':power.stateMask,'executionSpeedThrottleBit':bool(power.stateMask&1)}}
rig.record('native-desktop',info)
if len(sys.argv)>1 and sys.argv[1]=='capture':
    width,height=rect.right-rect.left,rect.bottom-rect.top
    g.CreateCompatibleDC.argtypes=[W.HDC];g.CreateCompatibleDC.restype=W.HDC;g.CreateCompatibleBitmap.argtypes=[W.HDC,C.c_int,C.c_int];g.CreateCompatibleBitmap.restype=W.HBITMAP
    g.SelectObject.argtypes=[W.HDC,W.HGDIOBJ];g.SelectObject.restype=W.HGDIOBJ;g.DeleteObject.argtypes=[W.HGDIOBJ];g.DeleteDC.argtypes=[W.HDC];u.ReleaseDC.argtypes=[W.HWND,W.HDC]
    g.BitBlt.argtypes=[W.HDC,C.c_int,C.c_int,C.c_int,C.c_int,W.HDC,C.c_int,C.c_int,W.DWORD];u.PrintWindow.argtypes=[W.HWND,W.HDC,W.UINT]
    class BI(C.Structure):_fields_=[('size',W.DWORD),('width',W.LONG),('height',W.LONG),('planes',W.WORD),('bits',W.WORD),('compression',W.DWORD),('imageSize',W.DWORD),('x',W.LONG),('y',W.LONG),('used',W.DWORD),('important',W.DWORD)]
    captures=[]
    for method in ['PrintWindow','BitBlt']:
        dc=u.GetWindowDC(hwnd); mem=g.CreateCompatibleDC(dc); bmp=g.CreateCompatibleBitmap(dc,width,height);old=g.SelectObject(mem,bmp)
        ok=u.PrintWindow(hwnd,mem,2) if method=='PrintWindow' else g.BitBlt(mem,0,0,width,height,dc,0,0,0x00CC0020)
        err=C.get_last_error();g.SelectObject(mem,old);bi=BI(C.sizeof(BI),width,-height,1,32,0,0,0,0,0,0);data=C.create_string_buffer(width*height*4)
        g.GetDIBits.argtypes=[W.HDC,W.HBITMAP,W.UINT,W.UINT,C.c_void_p,C.c_void_p,W.UINT];rows=g.GetDIBits(mem,bmp,0,height,data,C.byref(bi),0)
        im=Image.frombuffer('RGB',(width,height),data,'raw','BGRX',0,1);im.thumbnail((960,540));im.save(stage/(method+'.jpg'),quality=78)
        stat=ImageStat.Stat(im);captures.append({'method':method,'apiReturned':bool(ok),'lastErrorNonAuthoritative':err,'dibRows':rows,'meanRgb':stat.mean,'stdRgb':stat.stddev,'extrema':im.getextrema(),'file':method+'.jpg'})
        g.DeleteObject(bmp);g.DeleteDC(mem);u.ReleaseDC(hwnd,dc)
    rig.record('native-captures',captures);print(json.dumps(captures))
elif len(sys.argv)>1 and sys.argv[1]=='input':
    results=[];before=rig.probe('input-initial','environment',True)
    C.set_last_error(0);act=u.SetForegroundWindow(hwnd);err=C.get_last_error();fg=u.GetForegroundWindow();results.append({'method':'SetForegroundWindow','returned':bool(act),'lastError':err,'foregroundIsOwnedGame':windowpid(fg)==pid})
    # SendInput is global. Never inject into LockApp or any other process when focus acquisition fails.
    results.append({'method':'SendInput','attempted':False,'reason':'Target focus unavailable; global injection could reach a process this run does not own.'})
    point=(300 & 0xffff)|((300&0xffff)<<16)
    for name,msg,wp,lp in [('move',0x200,0,point),('left-down',0x201,1,point),('left-up',0x202,0,point),('wheel',0x20a,120<<16,((rect.left+300)&0xffff)|(((rect.top+300)&0xffff)<<16))]:
        pre=rig.probe('input-'+name+'-before','environment',True);C.set_last_error(0);posted=u.PostMessageW(hwnd,msg,wp,lp);e=C.get_last_error();time.sleep(.8);post=rig.probe('input-'+name+'-after','environment',True)
        results.append({'method':'PostMessageW','message':name,'posted':bool(posted),'error':e,'before':pre,'after':post})

    for v in results:
        for key in ['before','after']:
            if key in v:v[key].pop('assemblies',None)
    rig.record('native-input',results);print(json.dumps(results,indent=2))
else:print(json.dumps(info,indent=2))
