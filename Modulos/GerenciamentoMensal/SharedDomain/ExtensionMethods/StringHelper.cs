using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System;

public static class StringHelper
{
    public static bool PossuiValor(this string valor)
    {
        return !string.IsNullOrEmpty(valor) && !string.IsNullOrWhiteSpace(valor);
    }
}
