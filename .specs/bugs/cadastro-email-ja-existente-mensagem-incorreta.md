# Bug: cadastro de e-mail já existente retorna mensagem incorreta

- **Status:** Resolvido
- **Data:** 2026-09-09
- **Área:** API de login/cadastro

## Esperado

Ao tentar cadastrar um e-mail que já existe, a API deve rejeitar a operação com HTTP 422 e informar claramente que o e-mail já está cadastrado. E-mails em formato inválido devem continuar recebendo a mensagem de validação de formato.

## Observado

Para `matheushenriquesoares35@gmail.com`, a API retorna:

```json
{ "errors": ["E-mail invalido para cadastro!"] }
```

A mensagem descreve formato inválido, mas o e-mail possui formato válido e já existe na base.

## Reprodução determinística

Com o Compose local saudável:

```powershell
$body = @{ Email = 'matheushenriquesoares35@gmail.com'; Nome = 'Matheus teste' } | ConvertTo-Json
Invoke-WebRequest -Uri 'http://localhost:7170/api/login/Create' -Method Post -ContentType 'application/json' -Body $body -SkipHttpErrorCheck
```

Evidências da reprodução:

- HTTP 422.
- `db.Usuario.countDocuments({Email: 'matheushenriquesoares35@gmail.com'})` retornou `1`.
- O log do backend registrou `Tentativa de cadastro com e-mail ja existente`.

## Investigação

- A hipótese de formato inválido foi refutada: o mesmo e-mail é aceito pelo validador de domínio e foi localizado no MongoDB.
- A causa foi confirmada no ramo de duplicidade de `LoginService.CriarUsuario`, que devolve a mensagem de e-mail inválido para qualquer usuário encontrado.
- O erro de envio de código observado no Resend (`devmoreno.online` não verificado) é um problema operacional separado do texto desta resposta de cadastro.

## Correção aplicada

Foi alterada somente a mensagem do ramo de e-mail já existente para `E-mail já cadastrado!`, mantendo a rejeição HTTP 422 e o comportamento de validação de formato.

## Regressão

- Antes da correção, a asserção da mensagem desejada falhou com HTTP 422 e `E-mail invalido para cadastro!`.
- Depois da correção, a mesma requisição passou com HTTP 422 e `E-mail já cadastrado!`.
- A entrada `email-invalido` continuou distinta, retornando HTTP 400 e `E-mail informado invalido!`.
- O Compose permaneceu saudável com Mongo, API e frontend em estado `healthy`.
