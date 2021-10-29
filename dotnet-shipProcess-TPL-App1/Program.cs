using CommonModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Net.Http;
using System.Diagnostics;

namespace OrderProcessing
{
    class Program
    {
        static void Main(string[] args)
        {
            var degreeOfParallellism = 1;
            Console.WriteLine();

            var asyncPersistAction = new Func<ShipDetail, Task>(PersistToDatabase);

            //define the blocks
            var orderBuffer = new BufferBlock<Order>();
            var broadcaster = new BroadcastBlock<Order>(order => order);
            var upsBatcher = new BatchBlock<Order>(5);
            var fedexBatcher = new BatchBlock<Order>(5);
            var upsProcessor = new TransformManyBlock<Order[], ShipDetail>(orders => PostToCarrierAsync(CarrierType.Ups, orders));
            var fedexProcessor = new TransformManyBlock<Order[], ShipDetail>(orders => PostToCarrierAsync(CarrierType.Fedex, orders));
            var storageProcessor = new ActionBlock<ShipDetail>(asyncPersistAction, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = degreeOfParallellism });


            //link the blocks together
            orderBuffer.LinkTo(broadcaster);
            broadcaster.LinkTo(upsBatcher, order => order.Carrier == CarrierType.Ups);
            broadcaster.LinkTo(fedexBatcher, order => order.Carrier == CarrierType.Fedex);
            upsBatcher.LinkTo(upsProcessor);
            fedexBatcher.LinkTo(fedexProcessor);
            upsProcessor.LinkTo(storageProcessor);
            fedexProcessor.LinkTo(storageProcessor);

            //set the completion propagation rules

            orderBuffer.Completion.ContinueWith(t => broadcaster.Complete());
            broadcaster.Completion.ContinueWith(t =>
            {
                upsBatcher.Complete();
                fedexBatcher.Complete();
            });

            upsBatcher.Completion.ContinueWith(t => upsProcessor.Complete());
            fedexBatcher.Completion.ContinueWith(t => fedexProcessor.Complete());

            Action<Task> postOrderCompletion = t =>
            {
                Task.WaitAll(upsProcessor.Completion, fedexProcessor.Completion);
                storageProcessor.Complete();
            };
            upsProcessor.Completion.ContinueWith(postOrderCompletion);
            fedexProcessor.Completion.ContinueWith(postOrderCompletion);

            var s = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
                orderBuffer.Post(
                    new Order
                    {
                        Carrier = i % 2 == 0 ? CarrierType.Ups : CarrierType.Fedex,
                        OrderId = Guid.NewGuid(),
                        Items = new List<OrderItem>()
                    });

            orderBuffer.Complete(); // it needs to be complete so propergate completion
            storageProcessor.Completion.Wait();
            s.Stop();

            Console.WriteLine("Processing completed in {0}.", s.Elapsed);
        }


        // Simulates transformation of a list of Order objects into a service api model (ShipDetail).
        private static List<ShipDetail> CreateShipDetails(IEnumerable<Order> orders)
        {
            var shipDetails = orders.Select(order =>
                new ShipDetail
                {
                    ShipId = order.OrderId,
                    Items = order.Items.Select(item =>
                        new ShipItemInfo
                        {
                            Sku = item.Sku,
                        }).ToList()
                });

            return shipDetails.ToList();
        }

        // Sends orders to a shipping service endpoint dependent on the specified carrier
        private static async Task<IEnumerable<ShipDetail>> PostToCarrierAsync(CarrierType carrierType, Order[] orders)
        {
            var shipDetails = CreateShipDetails(orders);
            Console.WriteLine("Sending {0} orders to {1}.", orders.Length, carrierType);
            await Task.Delay(1);
            return shipDetails;
        }

        // Simulates persistence of tracking numbers for each item to a database.
        private static async Task PersistToDatabase(ShipDetail itemTrackingDetail)
        {
            // ...  your DB code here
            //Simulate updating the order to the database.
            await Task.Delay(50);
            Console.WriteLine("Wrote tracking details to DB for order.{0}", itemTrackingDetail.ShipId);
        }
    }
}
