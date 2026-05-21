# Implementation Plan — Custos Fixos Bugfix Compartilhamento

| Field        | Value                        |
| ------------ | ---------------------------- |
| Tech Lead    | @Usuario                     |
| Team         | Solo developer               |
| Epic/Ticket  | Custos Fixos — Bugfix        |
| Status       | Draft                        |
| Created      | 2026-05-21                   |
| Last Updated | 2026-05-21                   |

## Overview

Correção de bug no `CustoFixoLembreteService` onde usuários ativos em compartilhamentos de conta como convidado são excluídos do envio de lembretes de custos fixos, mesmo possuindo custos fixos vencendo na data de referência. O envio de e-mail de lembrete de custo fixo deve ocorrer independentemente do status de compartilhamento da conta do usuário.

**Technical Design**: [TECHNICAL-DESIGN.md](file:///d:/GitHub/FinanMap-Back-End/.specs/custos-fixos/TECHNICAL-DESIGN.md)

## Implementation Phases

### Phase 1: Correção do Service e Remoção de Dependência

**Goal**: Garantir que o processador de lembretes não filtre usuários convidados e remover a dependência não utilizada do `ICompartilhamentoRepository`.

**Vertical slice**: Application (CustoFixoLembreteService)

**Tasks**:

| ID | Task | Owner | Estimate |
|----|------|-------|----------|
| CFXB-P1-01 | Remover importação de `Domain.Compartilhamento.Repository;` em [CustoFixoLembreteService.cs](file:///d:/GitHub/FinanMap-Back-End/Modulos/GerenciamentoMensal/Application/CustoFixo/Service/CustoFixoLembreteService.cs) | @Usuario | 5m |
| CFXB-P1-02 | Remover campo privado `_compartilhamentoRepository` e parâmetro correspondente do construtor em [CustoFixoLembreteService.cs](file:///d:/GitHub/FinanMap-Back-End/Modulos/GerenciamentoMensal/Application/CustoFixo/Service/CustoFixoLembreteService.cs) | @Usuario | 10m |
| CFXB-P1-03 | Remover lógica de verificação `ehConvidadoAtivo` (linhas 101 a 109) em [CustoFixoLembreteService.cs](file:///d:/GitHub/FinanMap-Back-End/Modulos/GerenciamentoMensal/Application/CustoFixo/Service/CustoFixoLembreteService.cs) | @Usuario | 10m |

**Testing**:

- **Compilação**: Compilar o projeto `GerenciamentoMensal` com `dotnet build` para verificar se a assinatura alterada do construtor não quebrou registros manuais de injeção de dependência.

**Acceptance Criteria**:

- [ ] A aplicação compila sem erros.
- [ ] O serviço `CustoFixoLembreteService` não possui mais dependência de `ICompartilhamentoRepository`.
- [ ] O código que filtra usuários convidados foi removido.

**Dependencies**: Nenhuma.

---

## Milestones

| Milestone | Target Date | Description |
|-----------|-------------|-------------|
| Correção Aplicada | 2026-05-21 | Código corrigido e compilando sem erros localmente |

## Dependencies

| Dependency | Type | Owner | Status | Risk if Delayed |
|------------|------|-------|--------|-----------------|
| Nenhuma | - | - | - | - |

## Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Quebra de assinatura em testes inexistentes | Baixo | Baixa | O projeto não possui testes unitários automatizados configurados para esta classe, minimizando esse risco. |
| Usuário convidado receber e-mail indesejado | Médio | Baixa | O envio do e-mail é exclusivo para a conta titular e baseado na preferência opt-out global `ReceberNotificacoesCustosFixos`, que continua sendo validada. |
| Injeção de dependência falhar | Médio | Baixa | A injeção é resolvida automaticamente pelo scan de assembly do setup da aplicação. |

## Testing Strategy

Como o projeto não possui testes automatizados estruturados, a validação será feita por meio de compilação e verificação visual do código modificado.

## Rollback Plan

### Deployment Strategy
Deploy direto.

### Rollback Triggers
Se houver problemas na compilação ou inicialização do background service após o deploy.

### Rollback Steps
Reverter commits no Git para a versão anterior.
