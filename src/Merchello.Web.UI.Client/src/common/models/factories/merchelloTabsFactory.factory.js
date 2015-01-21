angular.module('merchello.models').factory('merchelloTabsFactory',
    ['MerchelloTabCollection',
        function(MerchelloTabCollection) {

            var Constructor = MerchelloTabCollection;

            // creates tabs for the sales listing page
            function createSalesListTabs() {
                var tabs = new Constructor();
                tabs.addTab('saleslist', 'Sales Listing', '#/merchello/merchello/saleslist/manage');
                return tabs;
            }

            // creates the tabs for sales overview section
            function createSalesTabs(invoiceKey) {
                var tabs = new Constructor();
                tabs.addTab('overview', 'Overview', '#/merchello/merchello/saleoverview/' + invoiceKey);
                tabs.addTab('payments', 'Payments', '#/merchello/merchello/invoicepayments/' + invoiceKey);
                tabs.addTab('shipments', 'Shipments', '#/merchello/merchello/ordershipments/' + invoiceKey);
                return tabs;
            }

            // creates the tabs for the gateway provider section
            function createGatewayProviderTabs() {
                var tabs = new Constructor();
                tabs.addTab('providers', 'Gateway Providers', '#/merchello/merchello/gatewayproviderlist/manage');
                tabs.addTab('notification', 'Notification', '#/merchello/merchello/notificationproviders/manage');
                tabs.addTab('payment', 'Payment', '#/merchello/merchello/paymentproviders/manage');
                tabs.addTab('shipping', 'Shipping', '#/merchello/merchello/shippingproviders/manage');
                tabs.addTab('taxation', 'Taxation', '#/merchello/merchello/taxationproviders/manage');
                return tabs;
            }

            function createReportsTabs() {
                var tabs = new Constructor();
                tabs.addTab('reportslist', 'Reports', '#/merchello/merchello/reportslist/manage');
                return tabs;
            }

            return {
                createSalesListTabs: createSalesListTabs,
                createSalesTabs: createSalesTabs,
                createGatewayProviderTabs: createGatewayProviderTabs,
                createReportsTabs: createReportsTabs
            };

}]);
