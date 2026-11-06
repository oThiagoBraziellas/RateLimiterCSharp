# RateLimiter em C#

Biblioteca de limitação de taxa (*rate limiting*) em C# (.NET 8) com três algoritmos: **token bucket**, **sliding window log** e **sliding window counter**, mais um `KeyedRateLimiter` para aplicar limites por usuário, IP ou chave de API. Todos aceitam um `TimeProvider` injetável, o que permite testar o comportamento temporal sem `Thread.Sleep`.

## Por que este projeto

Rate limiting protege APIs contra abuso e *thundering herds*, e é tema recorrente de system design ("projete um rate limiter") em empresas como Google, Stripe, Cloudflare e Amazon. Cada algoritmo tem trade-offs diferentes de precisão, memória e burst, e saber compará-los é o que se espera de um candidato sênior. O projeto também demonstra um ponto de engenharia muito valorizado: tornar código dependente de tempo testável com `TimeProvider` (nativo desde o .NET 8).

## Recursos

- `TokenBucketLimiter`: permite rajadas até a capacidade e impõe a taxa média; O(1) de tempo e memória.
- `SlidingWindowLogLimiter`: janela deslizante exata, guardando o timestamp de cada requisição aceita; sem rajadas na fronteira da janela.
- `SlidingWindowCounterLimiter`: aproximação com dois contadores ponderados; O(1) de memória.
- `KeyedRateLimiter<TKey>`: um limitador por chave, criado sob demanda.
- Resultado rico: `Allowed`, `Remaining` e `RetryAfter` (útil para o cabeçalho HTTP `Retry-After`).
- Consumo de vários permits por chamada; pedidos maiores que a capacidade retornam `RetryAfter` infinito.

## Como executar

Pré-requisito: .NET 8 SDK.

```bash
dotnet build
```

```csharp
var limiter = new TokenBucketLimiter(capacity: 10, refillPerSecond: 5);
var result = limiter.TryAcquire();
if (!result.Allowed) Console.WriteLine($"Tente novamente em {result.RetryAfter.TotalSeconds:F1}s");

var perUser = new KeyedRateLimiter<string>(() => new SlidingWindowLogLimiter(100, TimeSpan.FromMinutes(1)));
perUser.TryAcquire("user-42");
```

## Como testar

```bash
dotnet test
```

Os testes usam um `ManualTime` (subclasse de `TimeProvider`) para avançar o relógio de forma determinística, verificando recarga, teto de tokens, `RetryAfter` exato, ausência de burst na fronteira de janela, ponderação da janela anterior e uma corrida com 8 threads que garante que nunca se concede mais que a capacidade.

## Decisões de projeto

| Algoritmo | Tempo | Memória | Precisão | Burst |
|---|---|---|---|---|
| Token bucket | O(1) | O(1) | Exata para a taxa média | Até a capacidade |
| Sliding window log | O(1) amortizado | O(limite) | Exata | Nenhum |
| Sliding window counter | O(1) | O(1) | Aproximada | Pequeno |

- **Recarga contínua e preguiçosa.** O bucket não usa timer: calcula os tokens devidos a cada chamada a partir do tempo decorrido, em ponto flutuante, o que evita perda de frações de token.
- **Um `lock` por limitador.** A seção crítica é curtíssima; o `KeyedRateLimiter` usa `ConcurrentDictionary`, então chaves diferentes não competem entre si.
- **Estado local ao processo.** Para limites globais em vários nós, o estado teria de ir para Redis (script Lua ou `INCR` + `EXPIRE`); a interface `IRateLimiter` isola essa troca.
- **`RetryAfter` calculado, não estimado.** No log deslizante ele aponta para a expiração exata do hit necessário; no counter é um limite superior (fim da janela atual).

## Estrutura de pastas

```
RateLimiter/
├── RateLimiter.sln
├── src/RateLimiter/           # IRateLimiter, três algoritmos, KeyedRateLimiter
└── tests/RateLimiter.Tests/   # testes xUnit e ManualTime
```

## Limitações conhecidas

- Sem limpeza automática de chaves ociosas no `KeyedRateLimiter` (use `Forget`).
- Sem versão assíncrona que espere pelos permits (`WaitAsync`); a API só decide permitir ou negar.
- O log deslizante usa `Queue.ElementAt` para calcular o `RetryAfter` de pedidos com vários permits (O(n) apenas no caminho de negação).
- Não há integração pronta com ASP.NET Core; o middleware seria uma camada fina sobre `IRateLimiter`.

## Licença

MIT. Veja o arquivo [LICENSE](LICENSE).
