namespace AssemblyImporter.TCLR
{
    public class TCLRResource
    {
        const byte RESOURCE_Invalid = 0x00;
        const byte RESOURCE_Bitmap = 0x01;
        const byte RESOURCE_Font = 0x02;
        const byte RESOURCE_String = 0x03;
        const byte RESOURCE_Binary = 0x04;

        const byte FLAGS_PaddingMask = 0x03;
        const short SENTINEL_ID = 0x7FFF;

        // Sorted on id
        short id;
        byte kind;
        byte flags;
        uint offset;
    }
}
