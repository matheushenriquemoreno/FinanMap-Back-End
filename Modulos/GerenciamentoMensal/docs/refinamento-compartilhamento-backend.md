# 📋 Refinamento — Compartilhamento de Informações Entre Contas (Back-End)

> **Objetivo**: Permitir que um usuário compartilhe todas as informações do seu site (rendimentos, despesas, investimentos, dashboard, categorias) com outras contas, controlando o nível de permissão (Visualizar ou Editar), similar ao Google Docs/Sheets.

---

## 📖 Contexto

Atualmente, cada conta no FinanMap acessa **apenas seus próprios dados**. O `IUsuarioLogado` extrai o `Id` do usuário a partir do JWT, e todos os serviços filtram os dados por esse ID.

Com essa feature, o **Usuário A** poderá convidar o **Usuário B** (por e-mail) para acessar seus dados, definindo se B pode:
- **Visualizar** — ver todos os dados do Usuário A (somente leitura)
- **Editar** — ver e modificar os dados do Usuário A

Casos de uso: visão de casal, assessor financeiro/investimento.

---

## 🏗️ Arquitetura Existente (Resumo)

```
Domain/          → Entidades e Interfaces de Repositório
Application/     → DTOs, Interfaces de Serviço, Implementações de Serviço
Infra.data/      → Repositórios MongoDB, Mappings, Configuração
WebApi/          → Controllers (Minimal API), Interceptor (UsuarioLogado)
SharedDomain/    → EntityBase, IRepositoryBase<T>, Validators, Result Pattern
```

**Padrões importantes:**
- Entidades herdam de `EntityBase` (possui `Id: string`)
- Repositórios implementam `IRepositoryBase<T>` e herdam de `RepositoryMongoBase<T>`
- Serviços usam `IUsuarioLogado` para obter o ID/Usuario logado
- Controllers usam Minimal API (`MapGroup`, `MapGet`, `MapPost`, etc.)
- DI é automática via assembly scanning (`RegisterApplication`, `RegisterRepository`)
- Endpoints protegidos usam `.RequireAuthorization()`

---

## 📝 O Que Precisa Ser Criado

### 1. Domain Layer — Nova Entidade `Compartilhamento`

**Arquivo**: `Domain/Compartilhamento/Entity/Compartilhamento.cs`

```csharp
// Namespace: Domain.Compartilhamento.Entity (ou Domain.Entity se seguir o padrão atual)
public class Compartilhamento : EntityBase
{
    public string ProprietarioId { get; set; }   // ID do usuário que compartilha (dono dos dados)
    public string ConvidadoId { get; set; }       // ID do usuário que recebe acesso
    public string ConvidadoEmail { get; set; }    // E-mail do convidado (para exibição)
    public string ProprietarioEmail { get; set; } // E-mail do proprietário (para exibição)
    public string ProprietarioNome { get; set; }  // Nome do proprietário (para exibição)
    public NivelPermissao Permissao { get; set; } // Enum: Visualizar ou Editar
    public StatusConvite Status { get; set; }     // Enum: Pendente, Aceito, Recusado
    public DateTime DataCriacao { get; set; }
    public DateTime? DataAtualizacao { get; set; }
}
```

**Arquivo**: `Domain/Compartilhamento/Entity/NivelPermissao.cs`

```csharp
public enum NivelPermissao
{
    Visualizar = 0,  // Somente leitura
    Editar = 1       // Leitura e escrita
}
```

**Arquivo**: `Domain/Compartilhamento/Entity/StatusConvite.cs`

```csharp
public enum StatusConvite
{
    Pendente = 0,
    Aceito = 1,
    Recusado = 2
}
```

> **Validações no construtor** (seguir o padrão `DomainValidator`):
> - `ProprietarioId` não pode ser vazio
> - `ConvidadoEmail` deve ser um e-mail válido (usar `EmailValidator.IsValidEmail`)
> - `ProprietarioId` não pode ser igual ao `ConvidadoId` (não pode compartilhar consigo mesmo)
> - `Permissao` deve ser um valor válido do enum

---

### 2. Domain Layer — Interface do Repositório

**Arquivo**: `Domain/Compartilhamento/Repository/ICompartilhamentoRepository.cs`

```csharp
public interface ICompartilhamentoRepository : IRepositoryBase<Compartilhamento>
{
    /// Busca todos os compartilhamentos onde o usuário é o PROPRIETÁRIO (quem compartilhou)
    Task<List<Compartilhamento>> ObterPorProprietarioId(string proprietarioId);

    /// Busca todos os compartilhamentos onde o usuário é o CONVIDADO (quem recebeu acesso)
    Task<List<Compartilhamento>> ObterPorConvidadoId(string convidadoId);

    /// Busca um compartilhamento específico entre proprietário e convidado
    Task<Compartilhamento?> ObterPorProprietarioEConvidado(string proprietarioId, string convidadoId);

    /// Busca compartilhamentos pendentes para um e-mail (convidado ainda não tem conta ou não aceitou)
    Task<List<Compartilhamento>> ObterConvitesPendentesPorEmail(string email);
}
```

---

### 3. Application Layer — DTOs

**Arquivo**: `Application/Compartilhamento/DTOs/CriarCompartilhamentoDTO.cs`

```csharp
public class CriarCompartilhamentoDTO
{
    public string ConvidadoEmail { get; set; }    // E-mail da pessoa a ser convidada
    public NivelPermissao Permissao { get; set; } // Nível de permissão: Visualizar ou Editar
}
```

**Arquivo**: `Application/Compartilhamento/DTOs/AtualizarPermissaoDTO.cs`

```csharp
public class AtualizarPermissaoDTO
{
    public string CompartilhamentoId { get; set; }
    public NivelPermissao NovaPermissao { get; set; }
}
```

**Arquivo**: `Application/Compartilhamento/DTOs/ResponderConviteDTO.cs`

```csharp
public class ResponderConviteDTO
{
    public string CompartilhamentoId { get; set; }
    public bool Aceitar { get; set; } // true = Aceitar, false = Recusar
}
```

**Arquivo**: `Application/Compartilhamento/DTOs/ResultCompartilhamentoDTO.cs`

```csharp
public class ResultCompartilhamentoDTO
{
    public string Id { get; set; }
    public string ProprietarioId { get; set; }
    public string ProprietarioEmail { get; set; }
    public string ProprietarioNome { get; set; }
    public string ConvidadoId { get; set; }
    public string ConvidadoEmail { get; set; }
    public NivelPermissao Permissao { get; set; }
    public StatusConvite Status { get; set; }
    public DateTime DataCriacao { get; set; }
}
```

---

### 4. Application Layer — Interface do Serviço

**Arquivo**: `Application/Compartilhamento/Interfaces/ICompartilhamentoService.cs`

```csharp
public interface ICompartilhamentoService
{
    /// Cria um novo convite de compartilhamento
    Task<Result<ResultCompartilhamentoDTO>> Convidar(CriarCompartilhamentoDTO dto);

    /// Lista todos os compartilhamentos feitos PELO usuário logado (ele é o dono)
    Task<List<ResultCompartilhamentoDTO>> ObterMeusCompartilhamentos();

    /// Lista todos os convites recebidos PELO usuário logado
    Task<List<ResultCompartilhamentoDTO>> ObterConvitesRecebidos();

    /// Aceita ou recusa um convite recebido
    Task<Result> ResponderConvite(ResponderConviteDTO dto);

    /// Atualiza o nível de permissão de um compartilhamento existente
    Task<Result> AtualizarPermissao(AtualizarPermissaoDTO dto);

    /// Revoga (exclui) um compartilhamento — somente o proprietário pode fazer
    Task<Result> Revogar(string compartilhamentoId);
}
```

---

### 5. Application Layer — Implementação do Serviço

**Arquivo**: `Application/Compartilhamento/Service/CompartilhamentoService.cs`

Essa classe deve:
1. Injetar: `ICompartilhamentoRepository`, `IUsuarioLogado`, `IUsuarioRepository`
2. Seguir o padrão do `RendimentoService` como referência

**Regras de negócio para cada método:**

#### `Convidar(CriarCompartilhamentoDTO dto)`
1. Validar que o e-mail do convidado é diferente do e-mail do usuário logado
2. Buscar o usuário convidado pelo e-mail no `IUsuarioRepository`
3. Se o convidado **não existir** → retornar `Result.Failure` com erro "Usuário com este e-mail não encontrado"
4. Verificar se **já existe** um compartilhamento ativo entre proprietário e convidado (`ObterPorProprietarioEConvidado`)
5. Se já existir → retornar `Result.Failure` com erro "Já existe um compartilhamento com este usuário"
6. Criar a entidade `Compartilhamento` com:
   - `ProprietarioId` = `_usuarioLogado.Id`
   - `ConvidadoId` = id do usuário encontrado
   - `ConvidadoEmail` = e-mail do convidado
   - `ProprietarioEmail` = e-mail do proprietário (do `_usuarioLogado.Usuario`)
   - `ProprietarioNome` = nome do proprietário
   - `Status` = `Pendente`
   - `DataCriacao` = `DateTime.UtcNow`
7. Salvar no repositório
8. Retornar `Result.Success` com o DTO mapeado

#### `ObterMeusCompartilhamentos()`
1. Buscar por `ProprietarioId` = `_usuarioLogado.Id`
2. Mapear para lista de `ResultCompartilhamentoDTO`

#### `ObterConvitesRecebidos()`
1. Buscar por `ConvidadoId` = `_usuarioLogado.Id`
2. Mapear para lista de `ResultCompartilhamentoDTO`

#### `ResponderConvite(ResponderConviteDTO dto)`
1. Buscar o compartilhamento pelo `CompartilhamentoId`
2. Validar que o `ConvidadoId` é o `_usuarioLogado.Id` (apenas o convidado pode responder)
3. Validar que o `Status` atual é `Pendente`
4. Atualizar `Status` para `Aceito` ou `Recusado` conforme `dto.Aceitar`
5. Atualizar `DataAtualizacao`
6. Salvar no repositório

#### `AtualizarPermissao(AtualizarPermissaoDTO dto)`
1. Buscar o compartilhamento pelo `CompartilhamentoId`
2. Validar que o `ProprietarioId` é o `_usuarioLogado.Id` (apenas o dono pode alterar permissão)
3. Atualizar a `Permissao`
4. Atualizar `DataAtualizacao`
5. Salvar no repositório

#### `Revogar(string compartilhamentoId)`
1. Buscar o compartilhamento pelo `compartilhamentoId`
2. Validar que o `ProprietarioId` é o `_usuarioLogado.Id` (apenas o dono pode revogar)
3. Deletar do repositório

---

### 6. Infra Layer — MongoDB Mapping

**Arquivo**: `Infra.data/Mongo/Mappings/CompartilhamentoMapping.cs`

```csharp
public class CompartilhamentoMapping : IMongoMapping
{
    public void RegisterMap(IMongoClient mongoClient)
    {
        BsonClassMap.TryRegisterClassMap<Compartilhamento>(cm =>
        {
            cm.AutoMap();
            cm.MapMember(x => x.ProprietarioId).SetIsRequired(true);
            cm.MapMember(x => x.ConvidadoId).SetIsRequired(true);
            cm.MapMember(x => x.ConvidadoEmail).SetIsRequired(true);
            cm.MapMember(x => x.Permissao).SetIsRequired(true);
            cm.MapMember(x => x.Status).SetIsRequired(true);
        });
    }
}
```

---

### 7. Infra Layer — Repositório MongoDB

**Arquivo**: `Infra.data/Mongo/Repositorys/CompartilhamentoRepository.cs`

```csharp
public class CompartilhamentoRepository : RepositoryMongoBase<Compartilhamento>, ICompartilhamentoRepository
{
    public CompartilhamentoRepository(IMongoClient mongoClient) : base(mongoClient) { }

    public override string GetCollectionName() => "Compartilhamento";

    public async Task<List<Compartilhamento>> ObterPorProprietarioId(string proprietarioId)
    {
        return await _entityCollection
            .Find(x => x.ProprietarioId == proprietarioId)
            .ToListAsync();
    }

    public async Task<List<Compartilhamento>> ObterPorConvidadoId(string convidadoId)
    {
        return await _entityCollection
            .Find(x => x.ConvidadoId == convidadoId)
            .ToListAsync();
    }

    public async Task<Compartilhamento?> ObterPorProprietarioEConvidado(string proprietarioId, string convidadoId)
    {
        return await _entityCollection
            .Find(x => x.ProprietarioId == proprietarioId && x.ConvidadoId == convidadoId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Compartilhamento>> ObterConvitesPendentesPorEmail(string email)
    {
        return await _entityCollection
            .Find(x => x.ConvidadoEmail == email.ToLower() && x.Status == StatusConvite.Pendente)
            .ToListAsync();
    }
}
```

---

### 8. Infra Layer — Adicionar ao `IUsuarioRepository`

**Modificar**: `Domain/Usuario/Repository/IUsuarioRepository.cs`

> Verificar se já existe o método `GetByEmail`. Esse método é necessário para buscar o convidado pelo e-mail.
> Ele já existe em `UsuarioRepository.cs`, basta confirmar que a interface o expõe.

---

### 9. WebApi — Controller

**Arquivo**: `WebApi/Controllers/Compartilhamento.cs`

```csharp
public static class Compartilhamento
{
    public static RouteGroupBuilder MapCompartilhamentoEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var group = endpointRouteBuilder.MapGroup("/api/compartilhamento");

        // POST /api/compartilhamento — Criar convite de compartilhamento
        group.MapPost("", async (ICompartilhamentoService service, CriarCompartilhamentoDTO dto) =>
        {
            var result = await service.Convidar(dto);
            return result.MapResultCreated();
        });

        // GET /api/compartilhamento/meus — Listar meus compartilhamentos (onde sou o dono)
        group.MapGet("/meus", async (ICompartilhamentoService service) =>
        {
            var result = await service.ObterMeusCompartilhamentos();
            return Results.Ok(result);
        });

        // GET /api/compartilhamento/convites — Listar convites recebidos
        group.MapGet("/convites", async (ICompartilhamentoService service) =>
        {
            var result = await service.ObterConvitesRecebidos();
            return Results.Ok(result);
        });

        // POST /api/compartilhamento/responder — Aceitar ou recusar convite
        group.MapPost("/responder", async (ICompartilhamentoService service, ResponderConviteDTO dto) =>
        {
            var result = await service.ResponderConvite(dto);
            return result.MapResult();
        });

        // PUT /api/compartilhamento/permissao — Atualizar nível de permissão
        group.MapPut("/permissao", async (ICompartilhamentoService service, AtualizarPermissaoDTO dto) =>
        {
            var result = await service.AtualizarPermissao(dto);
            return result.MapResult();
        });

        // DELETE /api/compartilhamento/{id} — Revogar compartilhamento
        group.MapDelete("/{id:length(24)}", async (string id, ICompartilhamentoService service) =>
        {
            var result = await service.Revogar(id);
            return result.MapResult();
        });

        return group;
    }
}
```

---

### 10. WebApi — Registrar Endpoints

**Modificar**: `WebApi/Configs/EndpointConfiguration.cs`

Adicionar dentro de `MapProtectedEndpoints()`:

```csharp
app.MapCompartilhamentoEndpoints()
    .WithTags("Compartilhamento")
    .WithOpenApi()
    .RequireAuthorization();
```

---

### 11. ⭐ Modificação Crucial — Adaptar `IUsuarioLogado` para Suportar Contexto Compartilhado

Esta é a parte **mais importante**. Os serviços existentes (Rendimento, Despesa, Investimento, etc.) usam `_usuarioLogado.Id` para filtrar dados. Quando um usuário convidado acessa os dados do proprietário, o sistema precisa saber **de quem** buscar os dados.

#### Abordagem: Header `X-Proprietario-Id`

O front-end enviará um header HTTP customizado `X-Proprietario-Id` quando o convidado estiver visualizando dados de outro usuário.

**Modificar**: `Domain/Login/Interfaces/IUsuarioLogado.cs`

```csharp
public interface IUsuarioLogado
{
    /// ID do usuário autenticado (sempre do JWT)
    string Id { get; }

    /// Usuário autenticado
    Usuario Usuario { get; }

    /// ID do contexto de dados — será o ProprietarioId se estiver em modo compartilhado,
    /// ou o próprio Id se estiver vendo seus próprios dados
    string IdContextoDados { get; }

    /// Indica se o usuário está acessando dados de outro usuário
    bool EmModoCompartilhado { get; }

    /// Nível de permissão no contexto compartilhado (null se não estiver em modo compartilhado)
    NivelPermissao? PermissaoAtual { get; }
}
```

**Modificar**: `WebApi/Interceptor/UsuarioLogado.cs`

```csharp
public class UsuarioLogado : IUsuarioLogado
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ICompartilhamentoRepository _compartilhamentoRepository;

    public UsuarioLogado(
        IHttpContextAccessor httpContextAccessor,
        IUsuarioRepository usuarioRepository,
        ICompartilhamentoRepository compartilhamentoRepository)
    {
        _httpContextAccessor = httpContextAccessor;
        _usuarioRepository = usuarioRepository;
        _compartilhamentoRepository = compartilhamentoRepository;
    }

    // ... Id e Usuario ficam iguais ao que já existe ...

    public string IdContextoDados
    {
        get
        {
            // 1. Verifica se existe o header X-Proprietario-Id na requisição
            var proprietarioId = _httpContextAccessor.HttpContext?
                .Request.Headers["X-Proprietario-Id"]
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(proprietarioId))
            {
                // 2. Se existe, valida que o usuário logado TEM permissão (compartilhamento aceito)
                var compartilhamento = _compartilhamentoRepository
                    .ObterPorProprietarioEConvidado(proprietarioId, this.Id).Result;

                if (compartilhamento != null && compartilhamento.Status == StatusConvite.Aceito)
                    return proprietarioId; // ← Retorna o ID do PROPRIETÁRIO (quem compartilhou)

                throw new AutenticacaoNecessariaException(
                    "Você não tem permissão para acessar os dados deste usuário!");
            }

            return this.Id; // Se não há header, retorna o próprio ID
        }
    }

    public bool EmModoCompartilhado =>
        IdContextoDados != this.Id;

    public NivelPermissao? PermissaoAtual
    {
        get
        {
            if (!EmModoCompartilhado)
                return null;

            var proprietarioId = _httpContextAccessor.HttpContext?
                .Request.Headers["X-Proprietario-Id"]
                .FirstOrDefault();

            var compartilhamento = _compartilhamentoRepository
                .ObterPorProprietarioEConvidado(proprietarioId!, this.Id).Result;

            return compartilhamento?.Permissao;
        }
    }
}
```

---

### 12. Adaptar Serviços Existentes

Os serviços que fazem filtragem por `_usuarioLogado.Id` precisam passar a usar `_usuarioLogado.IdContextoDados` para buscar dados. Já nas operações de **escrita** (criar, editar, deletar), é necessário verificar a permissão.

**Exemplo de adaptação no** `RendimentoService.cs`:

```csharp
// ANTES (busca):
var rendimentos = await _rendimentoRepository.ObterPeloMes(mes, ano, _usuarioLogado.Id);

// DEPOIS (busca) — usa IdContextoDados:
var rendimentos = await _rendimentoRepository.ObterPeloMes(mes, ano, _usuarioLogado.IdContextoDados);
```

```csharp
// Operações de escrita — adicionar verificação:
public async Task<Result<ResultRendimentoDTO>> Adicionar(CreateRendimentoDTO createDTO)
{
    // Se em modo compartilhado, verificar se tem permissão de edição
    if (_usuarioLogado.EmModoCompartilhado && _usuarioLogado.PermissaoAtual != NivelPermissao.Editar)
        return Result.Failure<ResultRendimentoDTO>(
            Error.Forbidden("Você não tem permissão para editar os dados deste usuário."));

    // ... resto do código usando _usuarioLogado.IdContextoDados em vez de _usuarioLogado.Id
}
```

> **IMPORTANTE**: Essa adaptação precisa ser feita em **TODOS** os serviços que usam `_usuarioLogado.Id`:
> - `RendimentoService`
> - `DespesaService`
> - `InvestimentoService`
> - `CategoriaService`
> - `DashboardService` (se existir)
> - `AcumuladoMensalReport` (se usar `_usuarioLogado.Id`)

---

## 📊 Resumo dos Endpoints da API

| Método   | Rota                                  | Descrição                                  | Auth |
|----------|---------------------------------------|--------------------------------------------|------|
| `POST`   | `/api/compartilhamento`               | Convidar um usuário                        | ✅   |
| `GET`    | `/api/compartilhamento/meus`          | Listar meus compartilhamentos (sou o dono) | ✅   |
| `GET`    | `/api/compartilhamento/convites`      | Listar convites que recebi                 | ✅   |
| `POST`   | `/api/compartilhamento/responder`     | Aceitar/recusar convite                    | ✅   |
| `PUT`    | `/api/compartilhamento/permissao`     | Alterar permissão de um compartilhamento   | ✅   |
| `DELETE` | `/api/compartilhamento/{id}`          | Revogar compartilhamento                   | ✅   |

---

## 📁 Checklist de Arquivos

| Camada       | Ação    | Caminho do Arquivo                                              |
|--------------|---------|-----------------------------------------------------------------|
| Domain       | CRIAR   | `Domain/Compartilhamento/Entity/Compartilhamento.cs`            |
| Domain       | CRIAR   | `Domain/Compartilhamento/Entity/NivelPermissao.cs`              |
| Domain       | CRIAR   | `Domain/Compartilhamento/Entity/StatusConvite.cs`               |
| Domain       | CRIAR   | `Domain/Compartilhamento/Repository/ICompartilhamentoRepository.cs` |
| Domain       | VERIFICAR | `Domain/Usuario/Repository/IUsuarioRepository.cs` (verificar `GetByEmail`) |
| Domain       | MODIFICAR | `Domain/Login/Interfaces/IUsuarioLogado.cs`                   |
| Application  | CRIAR   | `Application/Compartilhamento/DTOs/CriarCompartilhamentoDTO.cs` |
| Application  | CRIAR   | `Application/Compartilhamento/DTOs/AtualizarPermissaoDTO.cs`    |
| Application  | CRIAR   | `Application/Compartilhamento/DTOs/ResponderConviteDTO.cs`      |
| Application  | CRIAR   | `Application/Compartilhamento/DTOs/ResultCompartilhamentoDTO.cs` |
| Application  | CRIAR   | `Application/Compartilhamento/Interfaces/ICompartilhamentoService.cs` |
| Application  | CRIAR   | `Application/Compartilhamento/Service/CompartilhamentoService.cs` |
| Application  | MODIFICAR | `Application/Rendimento/Service/RendimentoService.cs`          |
| Application  | MODIFICAR | `Application/Despesa/Service/DespesaService.cs`                |
| Application  | MODIFICAR | `Application/Investimento/Service/InvestimentoService.cs`      |
| Application  | MODIFICAR | `Application/Categoria/Service/CategoriaService.cs`            |
| Application  | MODIFICAR | `Application/Dashboard/...` (se existir service)               |
| Infra        | CRIAR   | `Infra.data/Mongo/Mappings/CompartilhamentoMapping.cs`          |
| Infra        | CRIAR   | `Infra.data/Mongo/Repositorys/CompartilhamentoRepository.cs`    |
| WebApi       | CRIAR   | `WebApi/Controllers/Compartilhamento.cs`                        |
| WebApi       | MODIFICAR | `WebApi/Configs/EndpointConfiguration.cs`                      |
| WebApi       | MODIFICAR | `WebApi/Interceptor/UsuarioLogado.cs`                          |

---

## 🔒 Regras de Segurança

1. **Apenas o proprietário** pode convidar, alterar permissões e revogar
2. **Apenas o convidado** pode aceitar/recusar um convite
3. **Convidado com permissão "Visualizar"** não pode criar/editar/deletar dados
4. **Convidado com permissão "Editar"** pode criar/editar/deletar dados do proprietário
5. O header `X-Proprietario-Id` é validado no `UsuarioLogado` — se o compartilhamento não existir ou não estiver aceito, a requisição é rejeitada
6. **Não é possível** compartilhar consigo mesmo

---

## 🧪 Sugestões de Teste

1. **Criar convite** → verificar que o registro é salvo no MongoDB
2. **Aceitar convite** → verificar que o status muda para `Aceito`
3. **Buscar dados como convidado** → usar header `X-Proprietario-Id` e verificar que retorna dados do proprietário
4. **Tentar editar sem permissão** → deve retornar erro 403
5. **Revogar por proprietário** → verificar que o registro é deletado
6. **Tentar revogar por convidado** → deve retornar erro
7. **Convidar e-mail inexistente** → deve retornar erro 404
8. **Convidar a si mesmo** → deve retornar erro 400

---

## 📌 Observações Importantes para o Desenvolvedor

1. **DI Automática**: Não precisa registrar manualmente os novos repositórios e serviços. O sistema de assembly scanning (`RegisterApplication` e `RegisterRepository`) faz isso automaticamente. Basta nomear a interface com `I` no início e a classe com o sufixo `Repository` ou sem prefixo.

2. **Mapping MongoDB**: O `CompartilhamentoMapping` será descoberto automaticamente se implementar `IMongoMapping`.

3. **Result Pattern**: Use `Result.Success()`, `Result.Failure()`, `Error.NotFound()`, `Error.Forbidden()` etc. conforme já usado no projeto (verifique os métodos disponíveis em `SharedDomain/ResultPattern/`).

4. **Mapster**: Pode usar `entity.Adapt<DTO>()` para mapeamento automático quando os nomes das propriedades coincidem.

5. **Ordem de implementação sugerida**:
   1. Enums e Entidade no Domain
   2. Interface do Repositório no Domain
   3. Mapping no Infra
   4. Repositório no Infra
   5. DTOs no Application
   6. Interface do Serviço no Application
   7. Implementação do Serviço no Application
   8. Controller no WebApi
   9. Registrar endpoint no `EndpointConfiguration.cs`
   10. Modificar `IUsuarioLogado` e `UsuarioLogado`
   11. Adaptar serviços existentes para usar `IdContextoDados`


# Resumo Visual do fluxo

┌─────────────────────────────────────────────────────────────────┐
│                         FRONT-END                               │
│                                                                 │
│  [Meus Dados ▾]  ←  Usuário clica e seleciona "Dados de João"   │
│       ↓                                                         │
│  Store: contextoAtivo = { proprietarioId: "abc123", ... }       │
│  localStorage: proprietarioIdAtivo = "abc123"                   │
│       ↓                                                         │
│  Watch detecta mudança → recarrega dados da página              │
│       ↓                                                         │
│  Axios interceptor: adiciona header X-Proprietario-Id: abc123   │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                    GET /api/Rendimentos?mes=2&ano=2026
                    Authorization: Bearer {meu-token}
                    X-Proprietario-Id: abc123
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                         BACK-END                                │
│                                                                 │
│  UsuarioLogado.IdContextoDados:                                 │
│    → Vê header X-Proprietario-Id = "abc123"                     │
│    → Busca compartilhamento: abc123 → meu-id? Status = Aceito?  │
│    → SIM → retorna "abc123" como IdContextoDados                │
│                                                                 │
│  RendimentoService.ObterRendimentoMes(mes, ano):                │
│    → Usa IdContextoDados = "abc123" (ID do João)                │
│    → Busca rendimentos do João no MongoDB                       │
│    → Retorna os dados do João para o front-end                  │
└─────────────────────────────────────────────────────────────────┘