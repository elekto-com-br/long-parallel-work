using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Elekto.Threading.Tasks
{
    /// <summary>
    /// Classe para execução em paralelo de uma grande quantidade de pequenas tarefas (exemplo: Simulação Monte-Carlo)
    /// </summary>
    public static class LongParallelWork
    {
        /// <summary>
        /// Resultados da execução
        /// </summary>
        public class WorkResult
        {
            /// <summary>
            /// Trabalho realizado
            /// </summary>
            public int WorkDone { get; set; } 

            /// <summary>
            /// Tempo consumido
            /// </summary>
            public TimeSpan TimeTaken { get; set; }

            /// <summary>
            /// Se a tarefa foi abortada ou não
            /// </summary>
            public bool Aborted { get; set; }
        }

        /// <summary>
        /// Executa em paralelo a tarefa
        /// </summary>
        /// <param name="workFunction">Função de trabalho, o índice variará de [0 ;totalWork[; não assuma nenhuma ordenação.</param>
        /// <param name="totalWork">Trabalho total</param>
        /// <param name="parallelFactor">Controla o paralelismo da execução.
        /// 0: Usa o paralelismo automático de ParallelOptions
        /// &gt;0: Usa paralelismo igual ao valor passado
        /// &lt;0: Usa paralelismo igual ao número total de CPUs da máquina menos o valor passado</param>
        /// <param name="idealBatchTimeSeconds">O tempo ideal de cada batch, em segundos. O algoritmo vai ajustar o tamanho de cada lote para tentar fazer com que os lotes (e notificações) sejam feitas neste tempo especificado.</param>
        /// <param name="initialBatchSize">Tamanho do primeiro lote.</param>
        /// <param name="progressFunction">Função para notificação do progresso, invocada ao final de cada batch.
        /// O primeiro parâmetro é o trabalho realizado; e o segundo é tempo total gasto até então.
        /// A função deve retornar um booleano, se falso o algoritmo abortará a execução.</param>
        /// <param name="messageFunction">Função para outras notificações relativas a execução da tarefa.</param>
        public static WorkResult DoWork(this Action<int> workFunction, int totalWork, int parallelFactor = 0, double idealBatchTimeSeconds = 10.0, int initialBatchSize = 100, 
            Func<int, TimeSpan, bool> progressFunction = null, Action<string> messageFunction = null)
        {
            var totalAvaiableCpus = Environment.ProcessorCount;
            messageFunction(string.Format("Máquina tem {0} CPUs totais.", totalAvaiableCpus));

            int maxDegreeOfParallelism;
            if (parallelFactor == 0)
            {
                maxDegreeOfParallelism = -1;
                if (messageFunction != null)
                {
                    messageFunction("Execução terá paralelismo automático.");
                }
            }
            else if (parallelFactor > 0)
            {
                maxDegreeOfParallelism = parallelFactor;
                if (messageFunction != null)
                {
                    messageFunction(string.Format("Execução terá paralelismo de {0} (configuração via máximo).",
                        maxDegreeOfParallelism));
                }
            }
            else
            {
                Debug.Assert(parallelFactor < 0);
                maxDegreeOfParallelism = Math.Max(totalAvaiableCpus + parallelFactor, 1);
                if (messageFunction != null)
                {                    
                    messageFunction(string.Format("Execução terá paralelismo de {0} (configuração via reserva).",
                        maxDegreeOfParallelism));
                }
            }
            var totalUsedCpus = maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : totalAvaiableCpus;

            var totalSw = Stopwatch.StartNew();
            var sw = new Stopwatch();
            var currentBatchSize = initialBatchSize;
            var index = 0;
            while (index < totalWork)
            {
                currentBatchSize = Math.Max(totalUsedCpus, currentBatchSize);
                var initial = index;
                var final = Math.Min(initial + currentBatchSize, totalWork);
                
                sw.Start();
                Parallel.For(initial, final, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                    workFunction);                
                sw.Stop();

                index = final;
                if (progressFunction != null)
                {
                    if (!progressFunction(index, totalSw.Elapsed))
                    {
                        if (messageFunction != null)
                        {
                            messageFunction("Execução abortada.");
                        }
                        return new WorkResult {Aborted = true, TimeTaken = totalSw.Elapsed, WorkDone = index};
                    }
                }

                if ((index < totalWork) &&
                    (Math.Abs(sw.Elapsed.TotalSeconds - idealBatchTimeSeconds) > (idealBatchTimeSeconds/10.0)))
                {
                    // Tenta ajustar o tamanho do batch
                    var factor = idealBatchTimeSeconds/sw.Elapsed.TotalSeconds;
                    factor = Math.Min(1000, factor);
                    factor = Math.Max(0.001, factor);
                    var previousBatchSyze = currentBatchSize;
                    currentBatchSize = (int) (currentBatchSize*factor);
                    if (currentBatchSize > totalWork - index)
                    {
                        // Para que o lote não seja maior que trabalho restante
                        currentBatchSize = totalWork - index;
                    }

                    // Para arredondar o tamanho de lote em múltiplos do tamanho inicial
                    currentBatchSize = (currentBatchSize/initialBatchSize+1)*initialBatchSize;

                    if ((messageFunction != null) && (previousBatchSyze != currentBatchSize))
                    {
                        messageFunction(string.Format("Tamanho do batch ajustado para {0:N0}.", currentBatchSize));
                    }
                }

                sw.Reset();
            }
            totalSw.Stop();

            if (messageFunction != null)
            {
                messageFunction(string.Format("Tudo feito em {0:N1}s.", totalSw.Elapsed.TotalSeconds));
            }

            return new WorkResult {Aborted = false, TimeTaken = totalSw.Elapsed, WorkDone = totalWork};
        }
    }
}