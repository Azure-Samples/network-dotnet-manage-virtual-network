// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Resources.Models;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;

namespace ManageVirtualNetwork
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;

        /**
         * Azure Network sample for managing virtual networks -
         *  - Create a virtual network with Subnets
         *  - Update a virtual network
         *  - Create virtual machines in the virtual network subnets
         *  - Create another virtual network
         *  - List virtual networks
         *  - Delete a virtual network.
         */
        public static void RunSample(ArmClient client)
        {
            string vnetName1 = Utilities.CreateRandomName("vnet1");
            string vnetName2 = Utilities.CreateRandomName("vnet2");
            string frontEndVmName = Utilities.CreateRandomName("fevm");
            string backEndVmName = Utilities.CreateRandomName("bevm");
            string publicIpAddressLeafDnsForFrontEndVm = Utilities.CreateRandomName("pip1");

            try
            {
                // Get default subscription
                SubscriptionResource subscription = client.GetDefaultSubscription();

                // Create a resource group in the EastUS region
                string rgName = Utilities.CreateRandomName("NetworkSampleRG");
                Utilities.Log($"Creating resource group...");
                ArmOperation<ResourceGroupResource> rgLro = subscription.GetResourceGroups().CreateOrUpdate(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log("Created a resource group with name: " + resourceGroup.Data.Name);

                //============================================================
                // Create a virtual network with specific address-space and two subnet

                // Creates a network security group for backend subnet

                Utilities.Log("Creating a network security group for virtual network backend subnet...");

                string backendNsgName = Utilities.CreateRandomName("backEndNSG");
                NetworkSecurityGroupData backendNsgInput = new NetworkSecurityGroupData()
                {
                    Location = resourceGroup.Data.Location,
                    SecurityRules =
                    {
                        new SecurityRuleData()
                        {
                            Name = "DenyInternetInComing",
                            Protocol = SecurityRuleProtocol.Asterisk,
                            SourcePortRange = "*",
                            DestinationPortRange = "*",
                            SourceAddressPrefix = "INTERNET",
                            DestinationAddressPrefix = "*",
                            Access = SecurityRuleAccess.Deny,
                            Priority = 100,
                            Direction = SecurityRuleDirection.Inbound,
                        },
                        new SecurityRuleData()
                        {
                            Name = "DenyInternetOutGoing",
                            Protocol = SecurityRuleProtocol.Asterisk,
                            SourcePortRange = "*",
                            DestinationPortRange = "*",
                            SourceAddressPrefix = "*",
                            DestinationAddressPrefix = "internet",
                            Access = SecurityRuleAccess.Deny,
                            Priority = 200,
                            Direction = SecurityRuleDirection.Outbound,
                        }
                    }

                };
                var backendNsgLro = resourceGroup.GetNetworkSecurityGroups().CreateOrUpdate(WaitUntil.Completed, backendNsgName, backendNsgInput);
                NetworkSecurityGroupResource backendNsg = backendNsgLro.Value;
                Utilities.Log($"Created network security group: {backendNsg.Data.Name}");

                // Create the virtual network with frontend and backend subnets, with
                // network security group rule applied to backend subnet]

                Utilities.Log("Creating virtual network #1...");

                string backendSubnetName = Utilities.CreateRandomName("besubnet");
                VirtualNetworkData vnetInput1 = new VirtualNetworkData()
                {
                    Location = resourceGroup.Data.Location,
                    AddressPrefixes = { "192.168.0.0/16" },
                    Subnets =
                    {
                        new SubnetData() { AddressPrefix = "192.168.1.0/24", Name = "subnet1" },
                        new SubnetData() { AddressPrefix = "192.168.2.0/24", Name = backendSubnetName, NetworkSecurityGroup = backendNsg.Data }
                    },
                };
                var vnetLro1 = resourceGroup.GetVirtualNetworks().CreateOrUpdate(WaitUntil.Completed, vnetName1, vnetInput1);
                VirtualNetworkResource vnet1 = vnetLro1.Value;
                SubnetData beSubnet = vnet1.Data.Subnets.First(item => item.Name == backendSubnetName);
                Utilities.Log($"Created a virtual network: {vnet1.Data.Name}");
                Utilities.Log($"vnet connected nsg: {beSubnet.NetworkSecurityGroup.Id.Name}");

                //============================================================
                // Update a virtual network

                // Creates a network security group for frontend subnet

                Utilities.Log("Creating a network security group for virtual network backend subnet...");

                string frontendNsgName = Utilities.CreateRandomName("frontEndNSG");
                NetworkSecurityGroupData frontendNsgInput = new NetworkSecurityGroupData()
                {
                    Location = resourceGroup.Data.Location,
                    SecurityRules =
                    {
                        new SecurityRuleData()
                        {
                            Name = "AllowHttpInComing",
                            Protocol = SecurityRuleProtocol.Tcp,
                            SourcePortRange = "*",
                            DestinationPortRange = "80",
                            SourceAddressPrefix = "INTERNET",
                            DestinationAddressPrefix = "*",
                            Access = SecurityRuleAccess.Allow,
                            Priority = 100,
                            Direction = SecurityRuleDirection.Inbound,
                        },
                        new SecurityRuleData()
                        {
                            Name = "DenyInternetOutGoing",
                            Protocol = SecurityRuleProtocol.Asterisk,
                            SourcePortRange = "*",
                            DestinationPortRange = "*",
                            SourceAddressPrefix = "*",
                            DestinationAddressPrefix = "internet",
                            Access = SecurityRuleAccess.Deny,
                            Priority = 200,
                            Direction = SecurityRuleDirection.Outbound,
                        }
                    }

                };
                var frontendNsgLro = resourceGroup.GetNetworkSecurityGroups().CreateOrUpdate(WaitUntil.Completed, frontendNsgName, frontendNsgInput);
                NetworkSecurityGroupResource frontendNsg = frontendNsgLro.Value;
                Utilities.Log($"Created network security group: {frontendNsg.Data.Name}");

                // Update the virtual network frontend subnet by associating it with network security group

                Utilities.Log("Associating network security group rule to frontend subnet");

                string frontendSubnetName = Utilities.CreateRandomName("fesubnet");
                vnetInput1 = new VirtualNetworkData()
                {
                    Location = resourceGroup.Data.Location,
                    AddressPrefixes = { "192.168.0.0/16" },
                    Subnets =
                    {
                        new SubnetData() { AddressPrefix = "192.168.1.0/24", Name = "subnet1" },
                        new SubnetData() { AddressPrefix = "192.168.2.0/24", Name = backendSubnetName, NetworkSecurityGroup = backendNsg.Data },
                        new SubnetData() { AddressPrefix = "192.168.3.0/24", Name = frontendSubnetName, NetworkSecurityGroup = frontendNsg.Data },
                    },
                };
                vnetLro1 = resourceGroup.GetVirtualNetworks().CreateOrUpdate(WaitUntil.Completed, vnetName1, vnetInput1);
                vnet1 = vnetLro1.Value;
                SubnetData feSubnet = vnet1.Data.Subnets.First(item => item.Name == frontendSubnetName);
                Utilities.Log("Network security group rule associated with the frontend subnet");
                Utilities.Log("vnet connected nsg: " + feSubnet.NetworkSecurityGroup.Id.Name);

                //============================================================
                // Create a virtual machine in each subnet

                // Creates the first virtual machine in frontend subnet

                Utilities.Log("Creating a virtual machine in the frontend subnet");
                var frontEndVM = Utilities.CreateVirtualMachine(resourceGroup, feSubnet.Id, frontEndVmName);
                Utilities.Log($"Created VM: {frontEndVM.Data.Name}");

                // Creates the second virtual machine in the backend subnet

                Utilities.Log("Creating a virtual machine in the backend subnet");
                var backEndVM = Utilities.CreateVirtualMachine(resourceGroup, beSubnet.Id, backEndVmName);
                Utilities.Log($"Created VM: {backEndVM.Data.Name}");

                //============================================================
                // Create a virtual network with default address-space and one default subnet

                Utilities.Log("Creating virtual network #2...");

                VirtualNetworkData vnetInput2 = new VirtualNetworkData()
                {
                    Location = resourceGroup.Data.Location,
                    AddressPrefixes = { "10.10.0.0/16" },
                    Subnets =
                    {
                        new SubnetData() { AddressPrefix = "10.10.1.0/24", Name = "default" },
                    },
                };
                var vnetLro2 = resourceGroup.GetVirtualNetworks().CreateOrUpdate(WaitUntil.Completed, vnetName2, vnetInput2);
                var vnet2 = vnetLro2.Value;
                Utilities.Log($"Created a virtual network: {vnet2.Data.Name}");

                //============================================================
                // List virtual networks

                Utilities.Log($"Get all virtual network under {resourceGroup.Data.Name}");
                foreach (var virtualNetwork in resourceGroup.GetVirtualNetworks().GetAll())
                {
                    Utilities.Log("\t" + virtualNetwork.Data.Name);
                }

                //============================================================
                // Delete a virtual network
                Utilities.Log("Deleting the virtual network");
                vnet2.Delete(WaitUntil.Completed);
                Utilities.Log("Deleted the virtual network");
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group...");
                        client.GetResourceGroupResource(_resourceGroupId).Delete(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId.Name}");
                    }
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception ex)
                {
                    Utilities.Log(ex);
                }
            }
        }

        public static void Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                RunSample(client);
            }
            catch (Exception ex)
            {
                Utilities.Log(ex);
            }
        }
    }
}