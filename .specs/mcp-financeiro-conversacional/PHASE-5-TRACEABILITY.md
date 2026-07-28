# Rastreabilidade da Fase 5 — importação estruturada

Data da verificação: 2026-07-28.

Legenda:

- `VERIFICADO`: há fluxo e asserção causal automatizada.
- `PARCIAL`: parte do requisito está coberta, mas existe gap explícito.
- `GAP`: não há evidência suficiente; não considerar a fase concluída por este item.

## MCP-72 a MCP-97

| ID | Estado | Evidência causal |
|---|---|---|
| MCP-72 | VERIFICADO | `McpImportContractTests.MCP_72_88_import_tools_have_closed_structured_contracts_and_safe_annotations`; `McpImportServicePhase5Tests.MCP_72_74_rejects_document_or_base64_content_before_persistence`. |
| MCP-73 | VERIFICADO | `McpImportServicePhase5Tests.MCP_75_80_previews_all_five_types_independently_encrypts_payload_and_aggregates_totals`; `MCP_73_accepts_exactly_1000_structured_items`. |
| MCP-74 | VERIFICADO | `McpImportPersistencePhase5UnitTests.Batch_and_item_preserve_owner_source_validation_result_and_operation_identity`; asserção de `SourceRef` no preview dos cinco tipos. |
| MCP-75 | VERIFICADO | `MCP_75_rejects_duplicate_client_item_id_before_any_partial_insert`; preview dos cinco tipos valida e persiste cada item. |
| MCP-76 | VERIFICADO | Asserções de `valid`, `invalid`, `pending`, `possible_duplicate` e `skipped` em `McpImportServicePhase5Tests`. |
| MCP-77 | VERIFICADO | `MCP_76_77_resolves_only_unique_compatible_category_name_and_suggests_ambiguous_matches`. |
| MCP-78 | VERIFICADO | O mesmo teste verifica sugestões com os IDs das correspondências ambíguas. |
| MCP-79 | VERIFICADO | `MCP_76_77_keeps_category_dependents_pending_until_category_creation` verifica categoria antes do dependente e posterior execução. |
| MCP-80 | VERIFICADO | Preview materializa `ErrorDetails` por item; testes de campos inválidos e dependência de categoria verificam código/campo. |
| MCP-81 | VERIFICADO | Asserções de `Suggestions`/orientação em limite, categoria ambígua e duplicidade. |
| MCP-82 | VERIFICADO | `MCP_82_84_blocks_owner_scoped_legacy_financial_duplicate_until_explicit_decision`. |
| MCP-83 | VERIFICADO | `MCP_78_79_marks_duplicates_explicitly_and_never_silently_discards_them` verifica `skip`. |
| MCP-84 | VERIFICADO | Testes de duplicidade intralote e legada verificam `import_anyway`. |
| MCP-85 | VERIFICADO | Preview dos cinco tipos verifica totais por tipo. |
| MCP-86 | VERIFICADO | Preview dos cinco tipos e duplicidades verifica contagens por estado. |
| MCP-87 | VERIFICADO | Asserções exatas de valores agregados de receita, despesa e investimento. |
| MCP-88 | VERIFICADO | Contrato fechado aceita somente `IMPORT_VALID_ITEMS`; teste de confirmação usa hash da prévia e replay. |
| MCP-89 | VERIFICADO | `MCP_81_85_confirms_only_valid_items_journal_before_effect_and_does_not_rerun_completed_items`. |
| MCP-90 | VERIFICADO | O mesmo teste mistura válido/inválido e verifica apenas um comando executado. |
| MCP-91 | VERIFICADO | Confirmação e reconciliação verificam `ExecutionState`, `OperationId` e `EntityId` por item. |
| MCP-92 | VERIFICADO | `MCP_92_preserves_actionable_domain_failure_message_and_guidance_per_item` verifica código, mensagem segura do domínio e guidance para corrigir/reenvia somente o item. |
| MCP-93 | VERIFICADO | `MCP_86_87_correction_accepts_only_correctable_items_and_marks_completed_as_already_applied`. |
| MCP-94 | VERIFICADO | Correção marca sucesso anterior como `already_applied`; replay de confirmação não reexecuta comando. |
| MCP-95 | VERIFICADO | `MCP_73_accepts_exactly_1000_structured_items` verifica exatamente 1.000 itens; 1.001 é rejeitado. |
| MCP-96 | VERIFICADO | JSON/base64/documentos são rejeitados antes de persistência; middleware rejeita multipart. |
| MCP-97 | VERIFICADO | Contrato não possui campo de documento e persiste somente payload estruturado protegido; conteúdo proibido não cria lote/item. |

## Complementos MCP-98 a MCP-105

| ID | Estado | Evidência causal |
|---|---|---|
| MCP-98 | VERIFICADO | `MCP_98_104_audits_preview_status_correction_and_rejections_with_safe_explicit_origin` verifica journal pré-execução e resultado/rejeição para preview, status, correção e confirmação. |
| MCP-99 | VERIFICADO | Journals e batches persistem `UserId`; testes de isolamento A/B e histórico Mongo verificam owner. |
| MCP-100 | VERIFICADO | Confirmação persiste `ToolName=finanmap_import_confirm`; DTO expõe o valor. |
| MCP-101 | VERIFICADO | Journal de confirmação usa allowlist `itemCount`/`entityType`; `McpHttpDtoTests.Audit_detail_maps_only_the_safe_write_allowlist`. |
| MCP-102 | VERIFICADO | `McpOperationJournal.StartedAtUtc` e DTO de auditoria possuem asserções de serialização. |
| MCP-103 | VERIFICADO | Passo/resultado/erro e resumo seguro são mapeados; `Audit_detail_maps_safe_import_batch_summary_expected_by_frontend`. |
| MCP-104 | VERIFICADO | O teste MCP-98/104 verifica `Origin` explícita com `clientId`, `protocolRevision` e `channel=mcp` em todas as invocações de importação. |
| MCP-105 | VERIFICADO | Mapper usa allowlist e o teste do lote prova remoção de `IMPORT_PAYLOAD_CANARY`; sanitizer genérico testa segredos. |

## MCP-115 a MCP-124

| ID | Estado | Evidência causal |
|---|---|---|
| MCP-115 | VERIFICADO | Limites, validação, duplicidade e persistência retornam envelopes/códigos estruturados. |
| MCP-116 | VERIFICADO | `McpFinancialReadPhase2Tests.Errors_distinguish_auth_scope_validation_capability_and_query_unavailability` e códigos específicos da importação. |
| MCP-117 | VERIFICADO | `MCP_88_status_is_owner_and_connection_scoped` retorna `NOT_FOUND` para outro owner sem revelar o lote. |
| MCP-118 | VERIFICADO | Testes causais para 1.001 itens e payload acima de 5 MiB verificam `LIMIT_EXCEEDED`. |
| MCP-119 | VERIFICADO | Os mesmos testes verificam máximo e guidance para dividir em lotes menores. |
| MCP-120 | VERIFICADO | Teste compartilhado de erros MCP verifica capacidade indisponível; registro das ferramentas é coberto por contrato. |
| MCP-121 | VERIFICADO | Ferramentas read-only e suas anotações são verificadas por `McpFinancialReadPhase2Tests`/`McpSdkTransportContractTests`. |
| MCP-122 | VERIFICADO | Contrato exige decisão fechada; testes Phase 3 verificam confirmação inválida/expirada sem efeito, e F5 só executa após `IMPORT_VALID_ITEMS`. |
| MCP-123 | VERIFICADO | `MCP_123_124_marks_item_unknown_when_final_journal_cas_fails_after_effect`. |
| MCP-124 | VERIFICADO | `MCP_123_124_reconciles_completed_journal_when_item_cas_fails_after_effect`; teste de efeito desconhecido verifica reconciliação por marcador sem retry. |

## Gaps da matriz

Nenhum gap permanece nos requisitos enumerados neste artefato. A conclusão da fase ainda depende dos gates e da revisão independente definidos no plano de implementação.

Este artefato não altera o `IMPLEMENTATION-STATE.md`; ele documenta apenas evidência executável observada no worktree.
