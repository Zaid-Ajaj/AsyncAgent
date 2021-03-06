﻿using AsyncAgentLib;
using AsyncAgentLib.Reactive;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
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

                    await TestReactiveAsyncAgent(stopwatch, ct, "ReactiveAsyncAgent");
                    await TestAsyncAgent(stopwatch, ct);
                    await TestAsyncAgentWithTwoParallelPushers(stopwatch, ct);

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

        static async Task TestReactiveAsyncAgent(Stopwatch stopwatch, CancellationToken ct, string name)
        {
            var reactiveAgent = GetNewReactiveAgent(stopwatch);
            stopwatch.Start();
            var completedMessagesTask = reactiveAgent.State.SkipWhile(state => state.ItemsCount < 1000000).FirstAsync();
            foreach (var msg in Enumerable.Range(1, 1000000))
            {
                reactiveAgent.Send(msg);
            }
            var latestState = await completedMessagesTask;
            stopwatch.Stop();
            WriteLine($"{name}{new string(' ', 30 - name.Length) }-> Sum for: [1..{latestState.ItemsCount}], Sum: {latestState.Sum}, Time: {stopwatch.ElapsedMilliseconds}ms");

            stopwatch.Reset();
            reactiveAgent.Dispose();
            await Task.Delay(1000, ct);
        }

        struct AgentState
        {
            public long ItemsCount { get; set; }
            public long Sum { get; set; }
        }

        static ReactiveAsyncAgent<AgentState, int> GetNewReactiveAgent(Stopwatch stopwatch)
        {
            return new ReactiveAsyncAgent<AgentState, int>(
                initialState: new AgentState { ItemsCount = 0, Sum = 0 },
                messageHandler: (state, msg, ct) =>
                {
                    ct.ThrowIfCancellationRequested();

                    state.Sum += msg;
                    state.ItemsCount = state.ItemsCount + 1;

                    return Task.FromResult(state);
                },
                errorHandler: (ex, ct) => Task.FromResult(false));
        }

        static async Task TestAsyncAgent(Stopwatch stopwatch, CancellationToken ct)
        {
            var asyncAgent = GetNewAgent(stopwatch, "AsyncAgent");
            stopwatch.Start();
            foreach (var msg in Enumerable.Range(1, 1000000))
            {
                asyncAgent.Send(msg);
            }

            await Task.Delay(1000, ct);
            stopwatch.Reset();
            asyncAgent.Dispose();
        }

        static async Task TestAsyncAgentWithTwoParallelPushers(Stopwatch stopwatch, CancellationToken ct)
        {
            var asyncAgent = GetNewAgent(stopwatch, "AsyncAgent2Pushers");
            stopwatch.Start();

            await Task.WhenAll(
                Task.Run(() =>
                {
                    foreach (var msg in Enumerable.Range(1, 500000))
                    {
                        asyncAgent.Send(msg);
                    }
                }), 
                Task.Run(() =>
                {
                    foreach (var msg in Enumerable.Range(500001, 500000))
                    {
                        asyncAgent.Send(msg);
                    }
                }));

            await Task.Delay(1000, ct);
            stopwatch.Reset();
            asyncAgent.Dispose();
        }

        static AsyncAgent<AgentState, int> GetNewAgent(Stopwatch stopwatch, string name)
        {
            return new AsyncAgent<AgentState, int>(
                initialState: new AgentState { ItemsCount = 0, Sum = 0 },
                messageHandler: (state, msg, ct) =>
                {
                    ct.ThrowIfCancellationRequested();

                    state.Sum += msg;
                    state.ItemsCount = state.ItemsCount + 1;

                    if (state.ItemsCount == 1000000)
                    {
                        stopwatch.Stop();
                        WriteLine($"{name}{new string(' ', 30 - name.Length)}-> Sum for: [1..{state.ItemsCount}], Sum: {state.Sum}, Time: {stopwatch.ElapsedMilliseconds}ms");
                        stopwatch.Reset();
                        state.ItemsCount = 0;
                        state.Sum = 0;
                    }

                    return Task.FromResult(state);
                },
                errorHandler: (ex, ct) => Task.FromResult(false));
        }
    }
}
