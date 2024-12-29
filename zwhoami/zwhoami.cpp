#include <windows.h>
#include <iostream>

#define GAP(X) GetAndPrint(L#X, X)

void GetAndPrint(const WCHAR* NameTypeStr, COMPUTER_NAME_FORMAT NameType)
{
    WCHAR buffer[1024];
    DWORD nSize = 1024;
    auto ok = ::GetComputerNameExW(NameType, buffer, &nSize);
    if (!ok)
    {
        std::cerr << "Error: GetLastError=" << ::GetLastError() << "\n";
    }
    else
    {
        printf("%ls=%ls\n", NameTypeStr, buffer);
    }
}

int main()
{
    GAP(ComputerNameNetBIOS);
    GAP(ComputerNameDnsHostname);
    GAP(ComputerNameDnsDomain);
    GAP(ComputerNameDnsFullyQualified);
    GAP(ComputerNamePhysicalNetBIOS);
    GAP(ComputerNamePhysicalDnsHostname);
    GAP(ComputerNamePhysicalDnsDomain);
    GAP(ComputerNamePhysicalDnsFullyQualified);
}
