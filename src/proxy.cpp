#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <d3d9.h>
#include <cstdint>
#include <cmath>

#pragma comment(lib, "d3d9.lib")

using Direct3DCreate9Fn = IDirect3D9* (WINAPI*)(UINT);
using CreateDeviceFn = HRESULT (STDMETHODCALLTYPE*)(IDirect3D9*, UINT, D3DDEVTYPE, HWND, DWORD, D3DPRESENT_PARAMETERS*, IDirect3DDevice9**);
using EndSceneFn = HRESULT (STDMETHODCALLTYPE*)(IDirect3DDevice9*);

static HMODULE g_realD3D9 = nullptr;
static Direct3DCreate9Fn g_realCreate9 = nullptr;
static CreateDeviceFn g_realCreateDevice = nullptr;
static EndSceneFn g_realEndScene = nullptr;
static volatile LONG g_hookedDevice = 0;
static volatile LONG g_drawEnabled = 1;

static void PatchVTable(void** table, size_t index, void* replacement, void** original)
{
    DWORD oldProtect = 0;
    if (!VirtualProtect(&table[index], sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtect)) return;
    if (original) *original = table[index];
    table[index] = replacement;
    DWORD ignored = 0;
    VirtualProtect(&table[index], sizeof(void*), oldProtect, &ignored);
    FlushInstructionCache(GetCurrentProcess(), &table[index], sizeof(void*));
}

struct Vertex { float x, y, z, rhw; D3DCOLOR color; };
static void AddLine(Vertex* out, int& n, float x1, float y1, float x2, float y2, D3DCOLOR c)
{
    out[n++] = { x1, y1, 0.0f, 1.0f, c };
    out[n++] = { x2, y2, 0.0f, 1.0f, c };
}

static void DrawProbeFigure(IDirect3DDevice9* device)
{
    if (!device || InterlockedCompareExchange(&g_drawEnabled, 1, 1) == 0) return;
    D3DVIEWPORT9 vp{};
    if (FAILED(device->GetViewport(&vp)) || vp.Width < 10 || vp.Height < 10) return;

    const float cx = vp.X + vp.Width * 0.78f;
    const float cy = vp.Y + vp.Height * 0.55f;
    const float s = (float)vp.Height / 720.0f;
    const D3DCOLOR cyan = D3DCOLOR_ARGB(165, 40, 220, 255);
    Vertex v[64]{}; int n = 0;

    const float r = 18.0f * s;
    for (int i = 0; i < 12; ++i) {
        float a0 = 6.28318530718f * i / 12.0f;
        float a1 = 6.28318530718f * (i + 1) / 12.0f;
        AddLine(v, n, cx + cosf(a0) * r, cy - 72.0f*s + sinf(a0)*r,
                      cx + cosf(a1) * r, cy - 72.0f*s + sinf(a1)*r, cyan);
    }
    AddLine(v,n,cx,cy-53*s,cx,cy+4*s,cyan);
    AddLine(v,n,cx-25*s,cy-42*s,cx+25*s,cy-42*s,cyan);
    AddLine(v,n,cx-25*s,cy-42*s,cx-39*s,cy-5*s,cyan);
    AddLine(v,n,cx+25*s,cy-42*s,cx+39*s,cy-5*s,cyan);
    AddLine(v,n,cx,cy+4*s,cx-20*s,cy+48*s,cyan);
    AddLine(v,n,cx,cy+4*s,cx+20*s,cy+48*s,cyan);
    AddLine(v,n,cx-20*s,cy+48*s,cx-25*s,cy+83*s,cyan);
    AddLine(v,n,cx+20*s,cy+48*s,cx+25*s,cy+83*s,cyan);

    IDirect3DStateBlock9* state = nullptr;
    if (FAILED(device->CreateStateBlock(D3DSBT_ALL, &state)) || !state) return;
    state->Capture();
    device->SetVertexShader(nullptr);
    device->SetPixelShader(nullptr);
    device->SetTexture(0, nullptr);
    device->SetFVF(D3DFVF_XYZRHW | D3DFVF_DIFFUSE);
    device->SetRenderState(D3DRS_LIGHTING, FALSE);
    device->SetRenderState(D3DRS_ZENABLE, FALSE);
    device->SetRenderState(D3DRS_ALPHABLENDENABLE, TRUE);
    device->SetRenderState(D3DRS_SRCBLEND, D3DBLEND_SRCALPHA);
    device->SetRenderState(D3DRS_DESTBLEND, D3DBLEND_INVSRCALPHA);
    device->SetRenderState(D3DRS_CULLMODE, D3DCULL_NONE);
    device->DrawPrimitiveUP(D3DPT_LINELIST, (UINT)(n / 2), v, sizeof(Vertex));
    state->Apply();
    state->Release();
}

static HRESULT STDMETHODCALLTYPE HookEndScene(IDirect3DDevice9* device)
{
    DrawProbeFigure(device);
    return g_realEndScene ? g_realEndScene(device) : D3D_OK;
}

static HRESULT STDMETHODCALLTYPE HookCreateDevice(IDirect3D9* self, UINT adapter, D3DDEVTYPE type,
    HWND focus, DWORD behavior, D3DPRESENT_PARAMETERS* pp, IDirect3DDevice9** outDevice)
{
    if (!g_realCreateDevice) return D3DERR_INVALIDCALL;
    HRESULT hr = g_realCreateDevice(self, adapter, type, focus, behavior, pp, outDevice);
    if (SUCCEEDED(hr) && outDevice && *outDevice && InterlockedCompareExchange(&g_hookedDevice, 1, 0) == 0) {
        void** deviceVtable = *reinterpret_cast<void***>(*outDevice);
        PatchVTable(deviceVtable, 42, reinterpret_cast<void*>(&HookEndScene), reinterpret_cast<void**>(&g_realEndScene));
    }
    return hr;
}

extern "C" __declspec(dllexport) IDirect3D9* WINAPI Direct3DCreate9(UINT sdkVersion)
{
    if (!g_realD3D9) {
        wchar_t path[MAX_PATH]{};
        UINT len = GetSystemDirectoryW(path, MAX_PATH);
        if (len == 0 || len >= MAX_PATH - 12) return nullptr;
        lstrcatW(path, L"\\d3d9.dll");
        g_realD3D9 = LoadLibraryW(path);
        if (!g_realD3D9) return nullptr;
        g_realCreate9 = reinterpret_cast<Direct3DCreate9Fn>(GetProcAddress(g_realD3D9, "Direct3DCreate9"));
    }
    if (!g_realCreate9) return nullptr;
    IDirect3D9* d3d = g_realCreate9(sdkVersion);
    if (d3d) {
        void** vtable = *reinterpret_cast<void***>(d3d);
        PatchVTable(vtable, 16, reinterpret_cast<void*>(&HookCreateDevice), reinterpret_cast<void**>(&g_realCreateDevice));
    }
    return d3d;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) DisableThreadLibraryCalls(instance);
    if (reason == DLL_PROCESS_DETACH && g_realD3D9) FreeLibrary(g_realD3D9);
    return TRUE;
}
