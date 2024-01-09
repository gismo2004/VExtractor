// Decompiled with JetBrains decompiler
// Type: ViessmannExtractor.Properties.Resources
// Assembly: ViessmannExtractor, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F23EC848-2AF5-475C-963E-899BCA3C133B
// Assembly location: C:\00_repos\private\ViessmannExtractor\ViessmannExtractor\bin\Debug\net8.0\ViessmannExtractor.dll

using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

#nullable disable
namespace ViessmannExtractor.Properties
{
  [GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
  [DebuggerNonUserCode]
  [CompilerGenerated]
  internal class Resources
  {
    private static ResourceManager resourceMan;
    private static CultureInfo resourceCulture;

    internal Resources()
    {
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    internal static ResourceManager ResourceManager
    {
      get
      {
        if (ViessmannExtractor.Properties.Resources.resourceMan == null)
          ViessmannExtractor.Properties.Resources.resourceMan = new ResourceManager("ViessmannExtractor.Properties.Resources", typeof (ViessmannExtractor.Properties.Resources).Assembly);
        return ViessmannExtractor.Properties.Resources.resourceMan;
      }
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    internal static CultureInfo Culture
    {
      get => ViessmannExtractor.Properties.Resources.resourceCulture;
      set => ViessmannExtractor.Properties.Resources.resourceCulture = value;
    }
  }
}
