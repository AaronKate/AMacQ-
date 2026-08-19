using System;
using System.IO;
using System.Reflection;
using System.Linq;

class Probe {
    static int Main(string[] args) {
        if (args.Length < 1) { Console.Error.WriteLine("usage: probe <path-to-obfuscar.exe>"); return 1; }
        var asm = Assembly.LoadFrom(args[0]);
        var t = asm.GetType("Obfuscar.Obfuscator+StringSqueeze");
        if (t == null) { Console.Error.WriteLine("StringSqueeze not found"); return 2; }
        var m = t.GetMethod("ProcessStrings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null) { Console.Error.WriteLine("ProcessStrings not found"); return 3; }
        var body = m.GetMethodBody();
        var il = body.GetILAsByteArray();
        Console.WriteLine("IL length: " + il.Length);
        var mod = t.Module;
        for (int i = 0; i < il.Length; i++) {
            byte op = il[i];
            if (op == 0x72) {
                int tok = BitConverter.ToInt32(il, i + 1);
                string s = null;
                try { s = mod.ResolveString(tok); } catch {}
                if (!string.IsNullOrEmpty(s)) Console.WriteLine("STR: " + s);
                i += 4;
            } else if (op == 0x28 || op == 0x6F || op == 0x2B) {
                int tok = BitConverter.ToInt32(il, i + 1);
                try {
                    var mb = mod.ResolveMethod(tok);
                    Console.WriteLine("CALL: " + mb.DeclaringType?.Name + "." + mb.Name);
                } catch {}
                i += 4;
            }
        }
        return 0;
    }
}
