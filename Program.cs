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

            {
                // Get default subscription
                SubscriptionResource subscription = client.GetDefaultSubscription();

                // Create a resource group in the EastUS region
                string rgName = Utilities.CreateRandomName("NetworkSampleRG");
                //rgName = "NetworkSampleRG7810";
                Utilities.Log($"Creating resource group...");
                ArmOperation<ResourceGroupResource> rgLro = subscription.GetResourceGroups().CreateOrUpdate(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log("Created a resource group with name: " + resourceGroup.Data.Name);

                //var nsgLroxx = resourceGroup.GetNetworkSecurityGroups().Get("nsg4519");

                //============================================================
                // Create a virtual network with specific address-space and two subnet

                // Creates a network security group for backend subnet

                Utilities.Log("Creating a network security group for virtual network backend subnet...");

                string backendNsgName = Utilities.CreateRandomName("nsg");
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
                var backendNsgLro = resourceGroup.GetNetworkSecurityGroups().CreateOrUpdateAsync(WaitUntil.Completed, backendNsgName, backendNsgInput);
                NetworkSecurityGroupResource backendNsg = backendNsgLro.Result.Value;
                Utilities.Log("Created network security group");

                // Print the network security group
                Utilities.Log();

                // Create the virtual network with frontend and backend subnets, with
                // network security group rule applied to backend subnet]

                Utilities.Log("Creating virtual network #1...");

                VirtualNetworkData vnetInput1 = new VirtualNetworkData()
                {
                    Location = resourceGroup.Data.Location,
                    AddressPrefixes = { "192.168.0.0/16" },
                    Subnets =
                    {
                        new SubnetData() { AddressPrefix = "192.168.1.0/24", Name = "subnet1" },
                        new SubnetData() { AddressPrefix = "192.168.2.0/24", Name = "subnet2", NetworkSecurityGroup = backendNsg.Data }
                    },
                };
                var vnetLro1 = resourceGroup.GetVirtualNetworks().CreateOrUpdate(WaitUntil.Completed, vnetName1, vnetInput1);
                VirtualNetworkResource vnet1 = vnetLro1.Value;
                Utilities.Log("Created a virtual network");

                // Print the virtual network details
                Utilities.Log();

                //============================================================
                // Update a virtual network

                // Creates a network security group for frontend subnet

                Utilities.Log("Creating a network security group for virtual network backend subnet...");

                string frontendNsgName = Utilities.CreateRandomName("nsg");
                NetworkSecurityGroupData frontendNsgInput = new NetworkSecurityGroupData()
                {
                    Location = resourceGroup.Data.Location,
                    SecurityRules =
                    {
                        new SecurityRuleData()
                        {
                            Name = "AllowHttpInComing",
                            Protocol = SecurityRuleProtocol.Tcp,
                            SourcePortRange = "internet",
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
                var frontendNsgLro = resourceGroup.GetNetworkSecurityGroups().CreateOrUpdateAsync(WaitUntil.Completed, backendNsgName, backendNsgInput);
                NetworkSecurityGroupResource frontendNsg = frontendNsgLro.Result.Value;
                Utilities.Log("Created network security group");

                // Print the network security group
                Utilities.Log();

                // Update the virtual network frontend subnet by associating it with network security group

                Utilities.Log("Associating network security group rule to frontend subnet");

                vnetInput1 = new VirtualNetworkData()
                {
                    Location = resourceGroup.Data.Location,
                    AddressPrefixes = { "192.168.0.0/16" },
                    Subnets =
                    {
                        new SubnetData() { AddressPrefix = "192.168.1.0/24", Name = "subnet1" },
                        new SubnetData() { AddressPrefix = "192.168.2.0/24", Name = "subnet2", NetworkSecurityGroup = frontendNsg.Data }
                    },
                };
                vnetLro1 = resourceGroup.GetVirtualNetworks().CreateOrUpdate(WaitUntil.Completed, vnetName1, vnetInput1);
                vnet1 = vnetLro1.Value;
                //virtualNetwork1.Update()
                //        .UpdateSubnet(VNet1FrontEndSubnetName)
                //            .WithExistingNetworkSecurityGroup(frontEndSubnetNsg)
                //            .Parent()
                //        .Apply();

                Utilities.Log("Network security group rule associated with the frontend subnet");
                // Print the virtual network details
                Utilities.Log();

                ////============================================================
                //// Create a virtual machine in each subnet

                //// Creates the first virtual machine in frontend subnet

                //Utilities.Log("Creating a Linux virtual machine in the frontend subnet");

                //var t1 = DateTime.UtcNow;

                //var frontEndVM = azure.VirtualMachines.Define(frontEndVmName)
                //        .WithRegion(Region.USEast)
                //        .WithExistingResourceGroup(ResourceGroupName)
                //        .WithExistingPrimaryNetwork(virtualNetwork1)
                //        .WithSubnet(VNet1FrontEndSubnetName)
                //        .WithPrimaryPrivateIPAddressDynamic()
                //        .WithNewPrimaryPublicIPAddress(publicIpAddressLeafDnsForFrontEndVm)
                //        .WithPopularLinuxImage(KnownLinuxVirtualMachineImage.UbuntuServer16_04_Lts)
                //        .WithRootUsername(UserName)
                //        .WithSsh(SshKey)
                //        .WithSize(VirtualMachineSizeTypes.Parse("Standard_D2a_v4"))
                //        .Create();

                //var t2 = DateTime.UtcNow;
                //Utilities.Log("Created Linux VM: (took "
                //        + (t2 - t1).TotalSeconds + " seconds) " + frontEndVM.Id);
                //// Print virtual machine details
                //Utilities.PrintVirtualMachine(frontEndVM);

                //// Creates the second virtual machine in the backend subnet

                //Utilities.Log("Creating a Linux virtual machine in the backend subnet");

                //var t3 = DateTime.UtcNow;

                //var backEndVM = azure.VirtualMachines.Define(backEndVmName)
                //        .WithRegion(Region.USEast)
                //        .WithExistingResourceGroup(ResourceGroupName)
                //        .WithExistingPrimaryNetwork(virtualNetwork1)
                //        .WithSubnet(VNet1BackEndSubnetName)
                //        .WithPrimaryPrivateIPAddressDynamic()
                //        .WithoutPrimaryPublicIPAddress()
                //        .WithPopularLinuxImage(KnownLinuxVirtualMachineImage.UbuntuServer16_04_Lts)
                //        .WithRootUsername(UserName)
                //        .WithSsh(SshKey)
                //        .WithSize(VirtualMachineSizeTypes.Parse("Standard_D2a_v4"))
                //        .Create();

                //var t4 = DateTime.UtcNow;
                //Utilities.Log("Created Linux VM: (took "
                //        + (t4 - t3).TotalSeconds + " seconds) " + backEndVM.Id);
                //// Print virtual machine details
                //Utilities.PrintVirtualMachine(backEndVM);

                ////============================================================
                //// Create a virtual network with default address-space and one default subnet

                //Utilities.Log("Creating virtual network #2...");

                //var virtualNetwork2 = azure.Networks.Define(vnetName2)
                //        .WithRegion(Region.USEast)
                //        .WithNewResourceGroup(ResourceGroupName)
                //        .Create();

                //Utilities.Log("Created a virtual network");
                //// Print the virtual network details
                //Utilities.PrintVirtualNetwork(virtualNetwork2);

                ////============================================================
                //// List virtual networks

                //foreach (var virtualNetwork in azure.Networks.ListByResourceGroup(ResourceGroupName))
                //{
                //    Utilities.PrintVirtualNetwork(virtualNetwork);
                //}

                ////============================================================
                //// Delete a virtual network
                //Utilities.Log("Deleting the virtual network");
                //azure.Networks.DeleteById(virtualNetwork2.Id);
                //Utilities.Log("Deleted the virtual network");
            }
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