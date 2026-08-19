#include <windows.h>
#include <wchar.h>

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previous, PWSTR commandLine, int showCommand) {
    WCHAR launcherPath[MAX_PATH];
    WCHAR rootPath[MAX_PATH];
    WCHAR appPath[MAX_PATH];
    WCHAR command[32768];
    STARTUPINFOW startupInfo = { sizeof(startupInfo) };
    PROCESS_INFORMATION processInfo = { 0 };

    if (GetModuleFileNameW(NULL, launcherPath, MAX_PATH) == 0) {
        return 1;
    }
    wcscpy_s(rootPath, MAX_PATH, launcherPath);
    WCHAR* separator = wcsrchr(rootPath, L'\\');
    if (separator == NULL) {
        return 1;
    }
    *separator = L'\0';
    swprintf_s(appPath, MAX_PATH, L"%s\\bin\\UtauV.exe", rootPath);
    swprintf_s(command, 32768, L"\"%s\" %s", appPath, commandLine);

    if (!CreateProcessW(appPath, command, NULL, NULL, FALSE, 0, NULL, rootPath, &startupInfo, &processInfo)) {
        return (int)GetLastError();
    }
    CloseHandle(processInfo.hThread);
    CloseHandle(processInfo.hProcess);
    return 0;
}
