namespace AssemblyImporter.TCLR
{
    class TCLRResourceFile
    {
        const uint CURRENT_VERSION = 2;

        uint version;
        uint sizeOfHeader;
        uint sizeOfResourceHeader;
        uint numberOfResources;
        TCLRString name;
        //ushort pad
        uint offset;          // TBL_Resource
    }
}
