// Shared demo company directory used for presentations and UI development.
export const usersMockData = [
  { id: 1, firstName: 'Ryan', lastName: 'Whitmore', email: 'Ryan.Whitmore@company.com', role: 'Administrator', department: 'Information Technology', status: 'Active', createdDate: '2026-02-20T09:00:00Z' },
  { id: 2, firstName: 'Natalie', lastName: 'Brooks', email: 'Natalie.Brooks@company.com', role: 'Administrator', department: 'Information Technology', status: 'Active', createdDate: '2026-03-02T09:00:00Z' },
  { id: 3, firstName: 'Lauren', lastName: 'Prescott', email: 'Lauren.Prescott@company.com', role: 'Manager', department: 'Operations', status: 'Active', createdDate: '2026-03-12T09:00:00Z' },
  { id: 4, firstName: 'David', lastName: 'Reynolds', email: 'David.Reynolds@company.com', role: 'Manager', department: 'Finance', status: 'Active', createdDate: '2026-03-18T09:00:00Z' },
  { id: 5, firstName: 'Jennifer', lastName: 'Collins', email: 'Jennifer.Collins@company.com', role: 'Manager', department: 'Customer Success', status: 'Active', createdDate: '2026-03-25T09:00:00Z' },
  { id: 6, firstName: 'Emily', lastName: 'Carter', email: 'Emily.Carter@company.com', role: 'IT Support Agent', department: 'Information Technology', status: 'Active', createdDate: '2026-05-18T09:00:00Z' },
  { id: 7, firstName: 'Michael', lastName: 'Thompson', email: 'Michael.Thompson@company.com', role: 'IT Support Agent', department: 'Information Technology', status: 'Active', createdDate: '2026-05-19T09:00:00Z' },
  { id: 8, firstName: 'Sarah', lastName: 'Collins', email: 'Sarah.Collins@company.com', role: 'IT Support Agent', department: 'Information Technology', status: 'Active', createdDate: '2026-05-20T09:00:00Z' },
  { id: 9, firstName: 'David', lastName: 'Anderson', email: 'David.Anderson@company.com', role: 'IT Support Agent', department: 'Information Technology', status: 'Active', createdDate: '2026-05-21T09:00:00Z' },
  { id: 10, firstName: 'Kevin', lastName: 'Brooks', email: 'Kevin.Brooks@company.com', role: 'IT Support Agent', department: 'Information Technology', status: 'Active', createdDate: '2026-05-22T09:00:00Z' },
  { id: 11, firstName: 'Emma', lastName: 'Wilson', email: 'Emma.Wilson@company.com', role: 'Employee', department: 'Customer Success', status: 'Active', createdDate: '2026-06-03T09:00:00Z' },
  { id: 12, firstName: 'James', lastName: 'Miller', email: 'James.Miller@company.com', role: 'Employee', department: 'Finance', status: 'Active', createdDate: '2026-06-04T09:00:00Z' },
  { id: 13, firstName: 'Chloe', lastName: 'Anderson', email: 'Chloe.Anderson@company.com', role: 'Employee', department: 'Marketing', status: 'Active', createdDate: '2026-06-05T09:00:00Z' },
  { id: 14, firstName: 'Noah', lastName: 'Richardson', email: 'Noah.Richardson@company.com', role: 'Employee', department: 'Sales', status: 'Active', createdDate: '2026-06-06T09:00:00Z' },
  { id: 15, firstName: 'Grace', lastName: 'Sullivan', email: 'Grace.Sullivan@company.com', role: 'Employee', department: 'Human Resources', status: 'Active', createdDate: '2026-06-07T09:00:00Z' },
  { id: 16, firstName: 'Olivia', lastName: 'Bennett', email: 'Olivia.Bennett@company.com', role: 'Employee', department: 'Finance', status: 'Active', createdDate: '2026-06-08T09:00:00Z' },
  { id: 17, firstName: 'Daniel', lastName: 'Brooks', email: 'Daniel.Brooks@company.com', role: 'Employee', department: 'Operations', status: 'Active', createdDate: '2026-06-11T09:00:00Z' },
  { id: 18, firstName: 'Sophia', lastName: 'Mitchell', email: 'Sophia.Mitchell@company.com', role: 'Employee', department: 'Marketing', status: 'Inactive', createdDate: '2026-04-27T09:00:00Z' },
  { id: 19, firstName: 'Ethan', lastName: 'Parker', email: 'Ethan.Parker@company.com', role: 'Employee', department: 'Sales', status: 'Active', createdDate: '2026-06-13T09:00:00Z' },
  { id: 20, firstName: 'Ava', lastName: 'Collins', email: 'Ava.Collins@company.com', role: 'Employee', department: 'Customer Success', status: 'Active', createdDate: '2026-06-14T09:00:00Z' },
  { id: 21, firstName: 'Benjamin', lastName: 'Foster', email: 'Benjamin.Foster@company.com', role: 'Employee', department: 'Operations', status: 'Active', createdDate: '2026-06-15T09:00:00Z' },
  { id: 22, firstName: 'Hannah', lastName: 'Cooper', email: 'Hannah.Cooper@company.com', role: 'Employee', department: 'Human Resources', status: 'Active', createdDate: '2026-06-16T09:00:00Z' },
  { id: 23, firstName: 'Lucas', lastName: 'Adams', email: 'Lucas.Adams@company.com', role: 'Employee', department: 'Operations', status: 'Active', createdDate: '2026-06-17T09:00:00Z' },
  { id: 24, firstName: 'Madison', lastName: 'Green', email: 'Madison.Green@company.com', role: 'Employee', department: 'Marketing', status: 'Active', createdDate: '2026-06-18T09:00:00Z' },
  { id: 25, firstName: 'Andrew', lastName: 'Turner', email: 'Andrew.Turner@company.com', role: 'Employee', department: 'Sales', status: 'Inactive', createdDate: '2026-06-19T09:00:00Z' },
]

export const getMockUserById = (id) =>
  usersMockData.find((user) => user.id === id)

export const getMockUserName = (id) => {
  const user = getMockUserById(id)
  return user ? `${user.firstName} ${user.lastName}` : 'Unknown user'
}

export const mockItAgents = usersMockData.filter(
  (user) => user.role === 'IT Support Agent',
)
