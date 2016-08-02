﻿using AsyncAgentLib;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;

namespace Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            var tokenSource = new CancellationTokenSource();
            TestPerformance(tokenSource.Token);
            ReadLine();
            tokenSource.Cancel();
        }

        private static Task TestPerformance(CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                var stopwatch = new Stopwatch();

                int test = 0;
                while (!ct.IsCancellationRequested)
                {
                    test += 1;

                    var agent = GetNewAgent(stopwatch);
                    stopwatch.Start();

                    foreach (var msg in Enumerable.Range(1, 1000000).AsParallel())
                    {
                        agent.Send(msg);
                    }

                    await Task.Delay(1000, ct);
                    agent.Dispose();

                    if (test % 10 == 0)
                    {
                        test = 0;
                        Clear();
                        await Task.Delay(1000, ct);
                        stopwatch.Reset();
                    }
                }
            }, ct);
        }

        struct AgentState
        {
            public long ItemsCount { get; set; }
            public long Sum { get; set; }
        }

        static AsyncAgent<AgentState, long> GetNewAgent(Stopwatch stopwatch)
        {
            return new AsyncAgent<AgentState, long>(
                initialState: new AgentState { ItemsCount = 0, Sum = 0 },
                messageHandler: async (state, msg, ct) =>
                {
                    await Task.Delay(0, ct);

                    state.Sum += msg;
                    state.ItemsCount = state.ItemsCount + 1;

                    if (state.ItemsCount == 1000000)
                    {
                        stopwatch.Stop();
                        WriteLine($"Sum for: [1..{state.ItemsCount}], Sum: {state.Sum}, Time: {stopwatch.ElapsedMilliseconds}ms");
                        stopwatch.Reset();
                        state.ItemsCount = 0;
                        state.Sum = 0;
                    }

                    return state;
                },
                errorHandler: ex => Task.FromResult(false));
        }
    }
}
