﻿using Application.Shared.DTOs;

namespace Application.DTOs;

public class CreateDespesaDTO : CreateTransacaoDTO
{
    public string IdDespesaAgrupadora { get; set; }
}
